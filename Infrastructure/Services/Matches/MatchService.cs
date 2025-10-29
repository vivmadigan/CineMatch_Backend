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
            Console.WriteLine($"[MatchService] ?????????????????????????????????????????");
            Console.WriteLine($"[MatchService] ?? Processing match request (MANUAL)");
            Console.WriteLine($"[MatchService]    Clicker (requestor): {requestorId}");
            Console.WriteLine($"[MatchService]    Target (who they want to match): {targetUserId}");
            Console.WriteLine($"[MatchService]  Movie: {tmdbId}");

            // Check if there's an existing INCOMING request (target ? requestor)
            // This means the target user already liked this movie and created a request TO us
            Console.WriteLine($"[MatchService]    Checking for incoming request: {targetUserId} ? {requestorId}");
            
            var incomingRequest = await _db.MatchRequests
           .FirstOrDefaultAsync(x =>
          x.RequestorId == targetUserId &&
            x.TargetUserId == requestorId &&
         x.TmdbId == tmdbId, ct);

         if (incomingRequest != null)
                  {
             // MUTUAL MATCH! The target user already sent us a request
               Console.WriteLine($"[MatchService] ?? MUTUAL MATCH DETECTED!");
             Console.WriteLine($"[MatchService]    Incoming request found: {targetUserId} ? {requestorId}");
         Console.WriteLine($"[MatchService]    Creating chat room...");

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

     // Remove both match requests (fulfilled)
      Console.WriteLine($"[MatchService]    Removing fulfilled match requests...");
    _db.MatchRequests.Remove(incomingRequest);

                // Also remove our outgoing request if it exists
     var outgoingRequest = await _db.MatchRequests
               .FirstOrDefaultAsync(x =>
      x.RequestorId == requestorId &&
  x.TargetUserId == targetUserId &&
   x.TmdbId == tmdbId, ct);

     if (outgoingRequest != null)
      {
        _db.MatchRequests.Remove(outgoingRequest);
     Console.WriteLine($"[MatchService]    Removed 2 match requests (bidirectional)");
  }
      else
       {
           Console.WriteLine($"[MatchService]    Removed 1 match request (incoming only)");
      }

       await _db.SaveChangesAsync(ct);

          Console.WriteLine($"[MatchService] ? Chat room created: {room.Id}");
        Console.WriteLine($"[MatchService]    Members: {requestorId}, {targetUserId}");

     // Send "It's a match!" notification to BOTH users
      Console.WriteLine($"[MatchService]    Sending mutual match notifications...");
       await SendMutualMatchNotificationAsync(requestorId, targetUserId, tmdbId, room.Id);

    Console.WriteLine($"[MatchService] ?????????????????????????????????????????\n");

       return new MatchResultDto
     {
     Matched = true,
    RoomId = room.Id
            };
     }

    // No incoming request exists - create our outgoing request
     Console.WriteLine($"[MatchService]    No incoming request found, creating outgoing request...");
    
            var existingOutgoing = await _db.MatchRequests
      .FirstOrDefaultAsync(x =>
   x.RequestorId == requestorId &&
          x.TargetUserId == targetUserId &&
      x.TmdbId == tmdbId, ct);

          if (existingOutgoing == null)
      {
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

       Console.WriteLine($"[MatchService] ? Match request created: {requestorId} ? {targetUserId} (pending acceptance)");
           Console.WriteLine($"[MatchService] ?????????????????????????????????????????\n");
      }
  else
 {
   Console.WriteLine($"[MatchService] ??  Match request already exists (idempotent)");
          Console.WriteLine($"[MatchService] ?????????????????????????????????????????\n");
    }

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
        /// Automatically create ONE-WAY match requests when a user likes a movie.
        /// Finds all other users who liked the same movie and creates requests FROM current user TO them.
        /// Other users will see these requests and can manually accept/decline.
        /// </summary>
        public async Task CreateAutoMatchRequestsAsync(string userId, int tmdbId, CancellationToken ct)
        {
            Console.WriteLine($"[MatchService] ?????????????????????????????????????????");
            Console.WriteLine($"[MatchService] ?? Creating ONE-WAY match requests");
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

       // Create ONE-WAY match requests (userId ? otherUserId)
         // Other users will see these requests and can accept/decline manually
       int requestsCreated = 0;
      int requestsSkipped = 0;

   foreach (var otherUserId in usersWhoLiked)
                {
          try
        {
   // Check if they already have a chat room together (already matched)
  var existingRoom = await _db.ChatMemberships
          .Where(m => m.UserId == userId || m.UserId == otherUserId)
    .GroupBy(m => m.RoomId)
          .Where(g => g.Count() == 2 && g.Select(m => m.UserId).Distinct().Count() == 2)
         .AnyAsync(ct);

 if (existingRoom)
          {
       Console.WriteLine($"[MatchService]    ??  Chat room already exists with {otherUserId}, skipping");
                 requestsSkipped++;
     continue;
     }

          // Check if request already exists
         var existingRequest = await _db.MatchRequests
         .FirstOrDefaultAsync(x =>
   x.RequestorId == userId &&
               x.TargetUserId == otherUserId &&
             x.TmdbId == tmdbId, ct);

       if (existingRequest == null)
    {
        var newRequest = new MatchRequest
                   {
   Id = Guid.NewGuid(),
        RequestorId = userId,
               TargetUserId = otherUserId,
  TmdbId = tmdbId,
          CreatedAt = DateTime.UtcNow
        };

   _db.MatchRequests.Add(newRequest);
      await _db.SaveChangesAsync(ct);
             requestsCreated++;

      Console.WriteLine($"[MatchService]    ? Created request: {userId} ? {otherUserId}");
   }
     else
            {
           Console.WriteLine($"[MatchService]  ??  Request already exists: {userId} ? {otherUserId}");
  requestsSkipped++;
         }
       }
        catch (Exception ex)
   {
          Console.WriteLine($"[MatchService]    ? Failed to create request for {otherUserId}: {ex.Message}");
   }
         }

         Console.WriteLine($"[MatchService] ?? Summary:");
     Console.WriteLine($"[MatchService]    New requests created: {requestsCreated}");
  Console.WriteLine($"[MatchService]    Skipped (existing/matched): {requestsSkipped}");
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
        /// Decline a match request from another user.
        /// Removes the incoming match request and optionally notifies the original requestor.
        /// </summary>
        public async Task DeclineMatchAsync(string declinerUserId, string requestorUserId, int tmdbId, CancellationToken ct)
        {
   Console.WriteLine($"[MatchService] ?????????????????????????????????????????");
        Console.WriteLine($"[MatchService] ? Declining match request");
            Console.WriteLine($"[MatchService]    Original requestor: {requestorUserId}");
            Console.WriteLine($"[MatchService]    Declining user: {declinerUserId}");
         Console.WriteLine($"[MatchService]  Movie: {tmdbId}");

            // Find and remove the incoming match request (requestor ? decliner)
        var incomingRequest = await _db.MatchRequests
  .FirstOrDefaultAsync(x =>
   x.RequestorId == requestorUserId &&
     x.TargetUserId == declinerUserId &&
        x.TmdbId == tmdbId, ct);

            if (incomingRequest != null)
         {
          _db.MatchRequests.Remove(incomingRequest);
  await _db.SaveChangesAsync(ct);

     Console.WriteLine($"[MatchService] ? Match request declined and removed");
          Console.WriteLine($"[MatchService]    Request {requestorUserId} ? {declinerUserId} deleted");

        // TODO: Optional - Send notification to requestor that match was declined
  // await SendMatchDeclinedNotificationAsync(requestorUserId, declinerUserId, tmdbId);
    }
    else
            {
 Console.WriteLine($"[MatchService] ??  No match request found to decline");
             Console.WriteLine($"[MatchService]    Checked for: {requestorUserId} ? {declinerUserId}");
            }

            Console.WriteLine($"[MatchService] ?????????????????????????????????????????\n");
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
