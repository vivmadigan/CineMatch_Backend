using Infrastructure.Data.Context;
using Infrastructure.Data.Entities;
using Infrastructure.Models.Matches;
using Infrastructure.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services.Matches
{
    public sealed class MatchService : IMatchService
    {
        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notificationService;

        public MatchService(ApplicationDbContext db, INotificationService notificationService)
        {
            _db = db;
            _notificationService = notificationService;
        }

        public async Task<IReadOnlyList<CandidateDto>> GetCandidatesAsync(string userId, int take, CancellationToken ct)
        {
            // 1) Get current user's liked movie IDs
            var myLikes = await _db.UserMovieLikes
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => x.TmdbId)
                .ToListAsync(ct);

            // If user has no likes, return empty list
            if (myLikes.Count == 0)
                return Array.Empty<CandidateDto>();

            // 2) Find other users who liked ANY of the same movies
            // Group by UserId, calculate overlap count, get latest like timestamp, collect shared movie IDs
            var candidates = await _db.UserMovieLikes
                .AsNoTracking()
                .Where(x => x.UserId != userId && myLikes.Contains(x.TmdbId))
                .GroupBy(x => x.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    OverlapCount = g.Count(),
                    Latest = g.Max(x => x.CreatedAt),
                    SharedMovieIds = g.Select(x => x.TmdbId).ToList()
                })
                .OrderByDescending(x => x.OverlapCount)
                .ThenByDescending(x => x.Latest)
                .Take(Math.Max(1, take))
                .ToListAsync(ct);

            // 3) Join with AspNetUsers to get DisplayName
            var userIds = candidates.Select(c => c.UserId).ToList();
            var users = await _db.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.DisplayName })
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

            // 4) Map to DTOs with DisplayName
            var result = candidates
                .Select(c => new CandidateDto
                {
                    UserId = c.UserId,
                    DisplayName = users.TryGetValue(c.UserId, out var name) ? name : "Unknown",
                    OverlapCount = c.OverlapCount,
                    SharedMovieIds = c.SharedMovieIds
                })
                .ToList();

            return result;
        }

        public async Task<MatchResultDto> RequestAsync(string requestorId, string targetUserId, int tmdbId, CancellationToken ct)
        {
            // 1) Check if reciprocal request exists (target?requestor, same tmdbId)
            var reciprocalRequest = await _db.MatchRequests
                .FirstOrDefaultAsync(x =>
                x.RequestorId == targetUserId &&
                x.TargetUserId == requestorId &&
                x.TmdbId == tmdbId, ct);

            if (reciprocalRequest != null)
            {
                // 2) Mutual match! Create chat room and memberships
                var room = new ChatRoom
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow
                };

                _db.ChatRooms.Add(room);

                var membership1 = new ChatMembership
                {
                    RoomId = room.Id,
                    UserId = requestorId,
                    IsActive = true,
                    JoinedAt = DateTime.UtcNow
                };

                var membership2 = new ChatMembership
                {
                    RoomId = room.Id,
                    UserId = targetUserId,
                    IsActive = true,
                    JoinedAt = DateTime.UtcNow
                };

                _db.ChatMemberships.Add(membership1);
                _db.ChatMemberships.Add(membership2);

                // Remove the reciprocal request (it's been fulfilled)
                _db.MatchRequests.Remove(reciprocalRequest);

                await _db.SaveChangesAsync(ct);

                return new MatchResultDto
                {
                    Matched = true,
                    RoomId = room.Id
                };
            }

            // 3) No reciprocal match yet. Check if we already sent this request (idempotency)
            var existingRequest = await _db.MatchRequests
                .FirstOrDefaultAsync(x =>
                 x.RequestorId == requestorId &&
                x.TargetUserId == targetUserId &&
                 x.TmdbId == tmdbId, ct);

            if (existingRequest == null)
            {
                // 4) Save new request
                var newRequest = new MatchRequest
                {
                    Id = Guid.NewGuid(),
                    RequestorId = requestorId,
                    TargetUserId = targetUserId,
                    TmdbId = tmdbId,
                    CreatedAt = DateTime.UtcNow
                };

                _db.MatchRequests.Add(newRequest);
                await _db.SaveChangesAsync(ct);

                // ? NEW: Send real-time notification to target user
                await SendMatchNotificationAsync(requestorId, targetUserId, tmdbId);
            }

            // Return no match (request saved or already existed)
            return new MatchResultDto
            {
                Matched = false,
                RoomId = null
            };
        }

        /// <summary>
        /// Send real-time match notification to target user.
        /// Runs asynchronously so it doesn't block the API response.
        /// </summary>
        private async Task SendMatchNotificationAsync(string requestorId, string targetUserId, int tmdbId)
        {
            // Run notification in background to avoid blocking API response
            _ = Task.Run(async () =>
            {
                try
                {
                    // Get requestor's display name
                    var requestor = await _db.Users
                        .AsNoTracking()
                        .Where(u => u.Id == requestorId)
                        .Select(u => new { u.Id, u.DisplayName })
                        .FirstOrDefaultAsync();

                    // Get shared movie title
                    var sharedMovie = await _db.UserMovieLikes
                        .AsNoTracking()
                        .Where(l => l.UserId == requestorId && l.TmdbId == tmdbId)
                        .Select(l => l.Title)
                        .FirstOrDefaultAsync();

                    // Build notification payload
                    var matchData = new
                    {
                        type = "newMatch",
                        matchId = $"match-{requestorId}-{targetUserId}",
                        user = new
                        {
                            id = requestorId,
                            displayName = requestor?.DisplayName ?? "Someone"
                        },
                        sharedMovieTitle = sharedMovie ?? "a movie you liked",
                        timestamp = DateTime.UtcNow
                    };

                    // Send notification via SignalR
                    await _notificationService.SendMatchNotificationAsync(targetUserId, matchData);
                    Console.WriteLine($"[MatchService] ? Sent match notification: {requestorId} ? {targetUserId} for movie {tmdbId}");
                }
                catch (Exception ex)
                {
                    // Log error but don't throw (notification failure shouldn't break match creation)
                    Console.WriteLine($"[MatchService] ? Failed to send match notification: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Automatically create match requests when a user likes a movie.
        /// Creates bidirectional match requests and detects mutual matches.
        /// If mutual match is detected, automatically creates a chat room.
        /// Sends real-time notifications to all matched users.
        /// </summary>
        public async Task CreateAutoMatchRequestsAsync(string userId, int tmdbId, CancellationToken ct)
        {
            Console.WriteLine($"[MatchService] ?????????????????????????????????????????");
            Console.WriteLine($"[MatchService] ?? Starting auto-match process");
            Console.WriteLine($"[MatchService]    User: {userId}");
            Console.WriteLine($"[MatchService]    Movie: {tmdbId}");

            try
            {
                // Find all users who already liked this movie (excluding current user)
                var usersWhoLiked = await _db.UserMovieLikes
                    .AsNoTracking()
                    .Where(x => x.TmdbId == tmdbId && x.UserId != userId)
                    .Select(x => x.UserId)
                    .Distinct()
                    .ToListAsync(ct);

                if (usersWhoLiked.Count == 0)
                {
                    Console.WriteLine($"[MatchService] ??  No other users have liked movie {tmdbId} yet");
                    Console.WriteLine($"[MatchService] ?????????????????????????????????????????\n");
                    return;
                }

                Console.WriteLine($"[MatchService] ? Found {usersWhoLiked.Count} user(s) who liked movie {tmdbId}:");
                foreach (var uid in usersWhoLiked)
                {
                    Console.WriteLine($"[MatchService]    • {uid}");
                }

                // Create match requests for each user
                int matchCount = 0;
                int mutualMatchCount = 0;

                foreach (var otherUserId in usersWhoLiked)
                {
                    try
                    {
                        Console.WriteLine($"[MatchService] ?? Processing match with user {otherUserId}...");

                        var (mutualMatch, roomId) = await CreateBidirectionalMatchRequestsAsync(userId, otherUserId, tmdbId, ct);

                        if (mutualMatch)
                        {
                            mutualMatchCount++;
                            Console.WriteLine($"[MatchService] ?? MUTUAL MATCH DETECTED!");
                            Console.WriteLine($"[MatchService]  Chat Room: {roomId}");
                            Console.WriteLine($"[MatchService]    Users: {userId} ? {otherUserId}");

                            await SendMutualMatchNotificationAsync(userId, otherUserId, tmdbId, roomId);
                        }
                        else
                        {
                            matchCount++;
                            Console.WriteLine($"[MatchService] ? Match request created: {userId} ? {otherUserId}");
                            await SendMatchNotificationAsync(userId, otherUserId, tmdbId);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[MatchService] ? Failed to create match with user {otherUserId}:");
                        Console.WriteLine($"[MatchService]    {ex.Message}");
                    }
                }

                Console.WriteLine($"[MatchService] ?? Summary:");
                Console.WriteLine($"[MatchService]    Regular matches: {matchCount}");
                Console.WriteLine($"[MatchService]    Mutual matches: {mutualMatchCount}");
                Console.WriteLine($"[MatchService] ?????????????????????????????????????????\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MatchService] ? CRITICAL ERROR in CreateAutoMatchRequestsAsync:");
                Console.WriteLine($"[MatchService]    Message: {ex.Message}");
                Console.WriteLine($"[MatchService]    Type: {ex.GetType().Name}");
                Console.WriteLine($"[MatchService]    Stack Trace:\n{ex.StackTrace}");
                Console.WriteLine($"[MatchService] ?????????????????????????????????????????\n");
                throw;
            }
        }

        /// <summary>
        /// Create bidirectional match requests and detect mutual matches.
        /// Creates: userId to otherUserId AND otherUserId to userId
        /// If both requests exist, creates a chat room automatically.
        /// </summary>
        /// <returns>Tuple: (isMutualMatch, chatRoomId)</returns>
        private async Task<(bool isMutualMatch, Guid? roomId)> CreateBidirectionalMatchRequestsAsync(
        string userId,
            string otherUserId,
            int tmdbId,
         CancellationToken ct)
        {
  Console.WriteLine($"[MatchService]    ?? Checking existing match requests...");

      // Check if requests already exist
        var existingRequest1 = await _db.MatchRequests
            .FirstOrDefaultAsync(x =>
  x.RequestorId == userId &&
      x.TargetUserId == otherUserId &&
            x.TmdbId == tmdbId, ct);

            var existingRequest2 = await _db.MatchRequests
                .FirstOrDefaultAsync(x =>
      x.RequestorId == otherUserId &&
            x.TargetUserId == userId &&
   x.TmdbId == tmdbId, ct);

            Console.WriteLine($"[MatchService]       Request {userId} to {otherUserId}: {(existingRequest1 != null ? "EXISTS" : "NONE")}");
            Console.WriteLine($"[MatchService]       Request {otherUserId} to {userId}: {(existingRequest2 != null ? "EXISTS" : "NONE")}");

       // Track if mutual match already existed BEFORE we create any new requests
            bool wasMutualMatchBefore = existingRequest1 != null && existingRequest2 != null;

            // Create first request (userId to otherUserId) if doesn't exist
   bool createdRequest1 = false;
      if (existingRequest1 == null)
  {
                var newRequest1 = new MatchRequest
                {
Id = Guid.NewGuid(),
           RequestorId = userId,
     TargetUserId = otherUserId,
  TmdbId = tmdbId,
 CreatedAt = DateTime.UtcNow
  };
    _db.MatchRequests.Add(newRequest1);
await _db.SaveChangesAsync(ct);
  createdRequest1 = true;
      Console.WriteLine($"[MatchService]       ? Created: {userId} to {otherUserId}");
            }

       // Create second request (otherUserId to userId) if doesn't exist
        bool createdRequest2 = false;
  if (existingRequest2 == null)
            {
        var newRequest2 = new MatchRequest
                {
          Id = Guid.NewGuid(),
      RequestorId = otherUserId,
          TargetUserId = userId,
 TmdbId = tmdbId,
           CreatedAt = DateTime.UtcNow
     };
           _db.MatchRequests.Add(newRequest2);
      await _db.SaveChangesAsync(ct);
      createdRequest2 = true;
            Console.WriteLine($"[MatchService]       ? Created: {otherUserId} to {userId}");
    }

         // Now check if BOTH requests exist (after potential creation)
         bool hasBothRequestsNow = (existingRequest1 != null || createdRequest1) &&
      (existingRequest2 != null || createdRequest2);

  // Only create chat room if this is a NEW mutual match
      if (hasBothRequestsNow && !wasMutualMatchBefore)
{
      Console.WriteLine($"[MatchService]       ?? Both requests now exist = NEW MUTUAL MATCH!");

    // Double-check if chat room already exists for these users
    var existingRoom = await _db.ChatMemberships
          .Where(m => m.UserId == userId || m.UserId == otherUserId)
   .GroupBy(m => m.RoomId)
 .Where(g => g.Count() == 2 && g.Select(m => m.UserId).Distinct().Count() == 2)
        .Select(g => g.Key)
        .FirstOrDefaultAsync(ct);

  if (existingRoom != Guid.Empty)
           {
          Console.WriteLine($"[MatchService]       ??  Chat room already exists: {existingRoom}");
      return (true, existingRoom);
            }

          // Create new chat room
       var roomId = await CreateMutualMatchAsync(userId, otherUserId, tmdbId, ct);
    return (true, roomId);
     }
            else if (wasMutualMatchBefore)
            {
         Console.WriteLine($"[MatchService]       ??  Mutual match already existed before this call");

            // Find the existing room for these users
        var existingRoom = await _db.ChatMemberships
.Where(m => m.UserId == userId || m.UserId == otherUserId)
 .GroupBy(m => m.RoomId)
    .Where(g => g.Count() == 2 && g.Select(m => m.UserId).Distinct().Count() == 2)
          .Select(g => g.Key)
         .FirstOrDefaultAsync(ct);

       return (true, existingRoom);
            }

 // Only one request exists - not a mutual match yet
  Console.WriteLine($"[MatchService]       ? Waiting for other user to like this movie");
   return (false, null);
        }

        /// <summary>
        /// Create a chat room for a mutual match.
        /// Called when both users have liked the same movie.
        /// </summary>
        private async Task<Guid> CreateMutualMatchAsync(string userId1, string userId2, int tmdbId, CancellationToken ct)
        {
            // Create chat room
            var room = new ChatRoom
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };
            _db.ChatRooms.Add(room);

            // Create memberships for both users
            var membership1 = new ChatMembership
            {
                RoomId = room.Id,
                UserId = userId1,
                IsActive = true,
                JoinedAt = DateTime.UtcNow
            };

            var membership2 = new ChatMembership
            {
                RoomId = room.Id,
                UserId = userId2,
                IsActive = true,
                JoinedAt = DateTime.UtcNow
            };

            _db.ChatMemberships.Add(membership1);
            _db.ChatMemberships.Add(membership2);

            // Remove the match requests (they've been fulfilled)
            var requestsToRemove = await _db.MatchRequests
           .Where(x =>
            (x.RequestorId == userId1 && x.TargetUserId == userId2 && x.TmdbId == tmdbId) ||
         (x.RequestorId == userId2 && x.TargetUserId == userId1 && x.TmdbId == tmdbId))
                     .ToListAsync(ct);

            _db.MatchRequests.RemoveRange(requestsToRemove);

            await _db.SaveChangesAsync(ct);

            Console.WriteLine($"[MatchService] ?? Created chat room {room.Id} for mutual match: {userId1} ? {userId2}");

            return room.Id;
        }

        /// <summary>
        /// Send "It's a match!" notification to BOTH users when mutual match is detected.
        /// Different from regular match notification - indicates chat room is ready.
        /// </summary>
        private async Task SendMutualMatchNotificationAsync(string userId1, string userId2, int tmdbId, Guid? roomId)
        {
            try
            {
                // Get both users' display names
                var users = await _db.Users
        .AsNoTracking()
        .Where(u => u.Id == userId1 || u.Id == userId2)
          .Select(u => new { u.Id, u.DisplayName })
        .ToDictionaryAsync(u => u.Id, u => u.DisplayName);

                // Get movie title
                var movieTitle = await _db.UserMovieLikes
          .AsNoTracking()
           .Where(l => l.TmdbId == tmdbId && (l.UserId == userId1 || l.UserId == userId2))
           .Select(l => l.Title)
         .FirstOrDefaultAsync() ?? "a movie you liked";

                // Send notification to user1
                var matchData1 = new
                {
                    type = "mutualMatch",
                    matchId = $"mutual-{userId1}-{userId2}",
                    roomId = roomId?.ToString(),
                    user = new
                    {
                        id = userId2,
                        displayName = users.TryGetValue(userId2, out var name2) ? name2 : "Someone"
                    },
                    sharedMovieTitle = movieTitle,
                    timestamp = DateTime.UtcNow
                };

                await _notificationService.SendMatchNotificationAsync(userId1, matchData1);
                Console.WriteLine($"[MatchService] ?? Sent mutual match notification to {userId1}");

                // Send notification to user2
                var matchData2 = new
                {
                    type = "mutualMatch",
                    matchId = $"mutual-{userId2}-{userId1}",
                    roomId = roomId?.ToString(),
                    user = new
                    {
                        id = userId1,
                        displayName = users.TryGetValue(userId1, out var name1) ? name1 : "Someone"
                    },
                    sharedMovieTitle = movieTitle,
                    timestamp = DateTime.UtcNow
                };

                await _notificationService.SendMatchNotificationAsync(userId2, matchData2);
                Console.WriteLine($"[MatchService] ?? Sent mutual match notification to {userId2}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MatchService] ? Failed to send mutual match notifications: {ex.Message}");
            }
        }
    }
}
