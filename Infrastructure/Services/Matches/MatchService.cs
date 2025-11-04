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
          Console.WriteLine($"[MatchService] ?? Fetching candidates for user {userId}...");

    // 1) Get current user's liked movie IDs
         var myLikes = await _db.UserMovieLikes
     .AsNoTracking()
    .Where(x => x.UserId == userId)
      .Select(x => x.TmdbId)
            .ToListAsync(ct);

  // If user has no likes, return empty list
      if (myLikes.Count == 0)
      {
     Console.WriteLine($"[MatchService] ??  User has no likes yet");
       return Array.Empty<CandidateDto>();
  }

    Console.WriteLine($"[MatchService] ? User has liked {myLikes.Count} movie(s)");

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

  if (candidates.Count == 0)
 {
      Console.WriteLine($"[MatchService] ??  No candidates found");
         return Array.Empty<CandidateDto>();
     }

     Console.WriteLine($"[MatchService] ? Found {candidates.Count} candidate(s)");

        // 3) Get all candidate user IDs for bulk queries
var candidateUserIds = candidates.Select(c => c.UserId).ToList();

        // 4) Bulk fetch: Users who already have chat rooms with current user (FILTER THESE OUT)
  var matchedUserIds = await _db.ChatMemberships
     .AsNoTracking()
    .Where(m => m.UserId == userId)
     .Join(_db.ChatMemberships.AsNoTracking(),
        m1 => m1.RoomId,
     m2 => m2.RoomId,
       (m1, m2) => m2.UserId)
  .Where(otherUserId => otherUserId != userId && candidateUserIds.Contains(otherUserId))
  .Distinct()
  .ToListAsync(ct);

       Console.WriteLine($"[MatchService]    Filtering out {matchedUserIds.Count} already-matched user(s)");

   // Filter out matched users from candidates
   candidates = candidates
     .Where(c => !matchedUserIds.Contains(c.UserId))
          .ToList();

   if (candidates.Count == 0)
    {
  Console.WriteLine($"[MatchService] ??  No candidates left after filtering matched users");
      return Array.Empty<CandidateDto>();
   }

     Console.WriteLine($"[MatchService] ? {candidates.Count} candidate(s) remaining after filter");

       // 5) Re-get candidate user IDs after filtering
     candidateUserIds = candidates.Select(c => c.UserId).ToList();

     // 6) Bulk fetch: User display names
   var users = await _db.Users
    .AsNoTracking()
           .Where(u => candidateUserIds.Contains(u.Id))
    .Select(u => new { u.Id, u.DisplayName })
     .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

  // 7) Bulk fetch: Match requests sent BY current user TO candidates
  // FIX: Group by TargetUserId and take the most recent request per user
var sentRequests = await _db.MatchRequests
      .AsNoTracking()
  .Where(mr => mr.RequestorId == userId && candidateUserIds.Contains(mr.TargetUserId))
      .ToListAsync(ct);

       // Group by TargetUserId and take most recent request (fixes duplicate key error)
       var sentRequestsDict = sentRequests
    .GroupBy(sr => sr.TargetUserId)
    .ToDictionary(
     g => g.Key,
       g => g.OrderByDescending(sr => sr.CreatedAt).First().CreatedAt
  );

 // 8) Bulk fetch: Match requests sent TO current user FROM candidates
       var receivedRequests = await _db.MatchRequests
     .AsNoTracking()
    .Where(mr => mr.TargetUserId == userId && candidateUserIds.Contains(mr.RequestorId))
      .Select(mr => mr.RequestorId)
     .Distinct()
    .ToListAsync(ct);

   // 9) FIX: Simplified mutual matches query - already filtered matched users above
  // Just check if any candidates appear in our chat memberships (shouldn't happen but safety check)
     var currentUserRooms = await _db.ChatMemberships
     .AsNoTracking()
      .Where(m => m.UserId == userId)
   .Select(m => m.RoomId)
          .ToListAsync(ct);

   var mutualMatches = currentUserRooms.Count > 0
    ? await _db.ChatMemberships
         .AsNoTracking()
         .Where(m => currentUserRooms.Contains(m.RoomId) && m.UserId != userId)
    .Select(m => m.UserId)
         .Distinct()
                .ToListAsync(ct)
           : new List<string>();

  Console.WriteLine($"[MatchService]    Sent requests: {sentRequestsDict.Count}");
  Console.WriteLine($"[MatchService]    Received requests: {receivedRequests.Count}");
    Console.WriteLine($"[MatchService]    Mutual matches: {mutualMatches.Count}");

          // 10) Bulk fetch: Shared movie details (title, poster, year)
    var allSharedMovieIds = candidates.SelectMany(c => c.SharedMovieIds).Distinct().ToList();
    var sharedMovieDetails = await _db.UserMovieLikes
        .AsNoTracking()
        .Where(ml => allSharedMovieIds.Contains(ml.TmdbId))
        .ToListAsync(ct);

    // Build movie dictionary IN MEMORY (not in EF query)
    var imageBaseUrl = "https://image.tmdb.org/t/p/";
    var sharedMoviesByTmdbId = sharedMovieDetails
        .GroupBy(ml => ml.TmdbId)
        .Select(g => g.First())
        .ToDictionary(
            ml => ml.TmdbId,
            ml => new SharedMovieDto
            {
   TmdbId = ml.TmdbId,
Title = ml.Title ?? "",
     PosterUrl = string.IsNullOrWhiteSpace(ml.PosterPath)
             ? ""
  : $"{imageBaseUrl}w342{ml.PosterPath}",
         ReleaseYear = ml.ReleaseYear
       });

  // 11) Map to DTOs with all new fields
   var result = candidates
        .Select(c =>
{
  // Determine match status
         var hasSent = sentRequestsDict.ContainsKey(c.UserId);
     var hasReceived = receivedRequests.Contains(c.UserId);
   var isMatched = mutualMatches.Contains(c.UserId);

     string matchStatus;
     if (isMatched)
     {
  matchStatus = "matched";
    }
      else if (hasSent && hasReceived)
   {
       // Both sent requests = effectively matched (rare edge case)
 matchStatus = "matched";
      }
     else if (hasSent)
  {
      matchStatus = "pending_sent";
       }
  else if (hasReceived)
      {
     matchStatus = "pending_received";
 }
else
    {
  matchStatus = "none";
     }

// Get shared movie details
  var sharedMovies = c.SharedMovieIds
     .Where(tmdbId => sharedMoviesByTmdbId.ContainsKey(tmdbId))
     .Select(tmdbId => sharedMoviesByTmdbId[tmdbId])
    .ToList();

       // Get request sent timestamp (if exists)
  DateTime? requestSentAt = hasSent ? sentRequestsDict[c.UserId] : null;

    return new CandidateDto
    {
  UserId = c.UserId,
    DisplayName = users.TryGetValue(c.UserId, out var name) ? name : "Unknown",
 OverlapCount = c.OverlapCount,
      SharedMovieIds = c.SharedMovieIds,
      SharedMovies = sharedMovies,
     MatchStatus = matchStatus,
RequestSentAt = requestSentAt
         };
     })
    .ToList();

     Console.WriteLine($"[MatchService] ? Returning {result.Count} candidate(s) with full details");

    return result;
        }

        public async Task<MatchResultDto> RequestAsync(string requestorId, string targetUserId, int tmdbId, CancellationToken ct)
        {
      Console.WriteLine($"[MatchService] ?????????????????????????????????????????????????????????");
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

              // ?? RACE CONDITION PROTECTION: Use transaction
 using var transaction = await _db.Database.BeginTransactionAsync(ct);
       try
  {
      // Check if room already exists (idempotent)
     var existingRoom = await _db.ChatMemberships
 .AsNoTracking()
  .Where(m => m.UserId == requestorId || m.UserId == targetUserId)
         .GroupBy(m => m.RoomId)
            .Where(g => g.Count() == 2 && g.Select(m => m.UserId).Distinct().Count() == 2)
       .Select(g => g.Key)
.FirstOrDefaultAsync(ct);

if (existingRoom != Guid.Empty)
        {
       Console.WriteLine($"[MatchService] ??  Chat room already exists: {existingRoom}");
              await transaction.CommitAsync(ct);
  return new MatchResultDto
           {
Matched = true,
   RoomId = existingRoom
    };
             }

  // Create new room
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
        await transaction.CommitAsync(ct);

            Console.WriteLine($"[MatchService] ? Chat room created: {room.Id}");
    Console.WriteLine($"[MatchService]    Members: {requestorId}, {targetUserId}");

             // Send "It's a match!" notification to BOTH users
       Console.WriteLine($"[MatchService]    Sending mutual match notifications...");
       await SendMutualMatchNotificationAsync(requestorId, targetUserId, tmdbId, room.Id);

         Console.WriteLine($"[MatchService] ?????????????????????????????????????????????????????????\n");

         return new MatchResultDto
        {
            Matched = true,
 RoomId = room.Id
       };
           }
           catch (Exception ex)
        {
              Console.WriteLine($"[MatchService] ? Transaction failed: {ex.Message}");
     await transaction.RollbackAsync(ct);
     throw;
    }
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
            // ?? RACE CONDITION PROTECTION: Database unique constraint will prevent duplicates
 try
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

       // ?? NEW: Send real-time notification to target user that they received a match request
      Console.WriteLine($"[MatchService]    Sending match request received notification...");
            await SendMatchRequestReceivedNotificationAsync(requestorId, targetUserId, tmdbId);

        Console.WriteLine($"[MatchService] ?????????????????????????????????????????????????????????\n");
                }
      catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_MatchRequests_UniqueRequest") == true)
     {
          // Race condition detected: Another request beat us to it
       Console.WriteLine($"[MatchService] ??  Match request already exists (race condition prevented by database)");
   Console.WriteLine($"[MatchService] ?????????????????????????????????????????????????????????\n");
         }
      }
  else
 {
   Console.WriteLine($"[MatchService] ??  Match request already exists (idempotent)");
       Console.WriteLine($"[MatchService] ?????????????????????????????????????????????????????????\n");
  }

  return new MatchResultDto
          {
  Matched = false,
     RoomId = null
         };
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
        /// Get all active matches for a user (users they have chat rooms with).
   /// Includes last message preview, unread count, and shared movies.
 /// </summary>
  public async Task<IReadOnlyList<ActiveMatchDto>> GetActiveMatchesAsync(string userId, CancellationToken ct)
     {
     Console.WriteLine($"[MatchService] ?? Fetching active matches for user {userId}...");

      // 1) Get all chat rooms current user is a member of
  var userRooms = await _db.ChatMemberships
        .AsNoTracking()
     .Where(m => m.UserId == userId && m.IsActive)
    .Select(m => m.RoomId)
     .ToListAsync(ct);

 if (userRooms.Count == 0)
    {
    Console.WriteLine($"[MatchService] ??  User has no active matches");
   return Array.Empty<ActiveMatchDto>();
  }

    Console.WriteLine($"[MatchService] ? User is in {userRooms.Count} chat room(s)");

            // 2) Get the other user in each room with basic info
  var roomMemberships = await _db.ChatMemberships
 .AsNoTracking()
          .Where(m => userRooms.Contains(m.RoomId) && m.UserId != userId)
   .Select(m => new 
   { 
       m.RoomId, 
m.UserId, 
       m.JoinedAt,
       DisplayName = m.User != null ? m.User.DisplayName : "Unknown",
    RoomCreatedAt = m.Room != null ? m.Room.CreatedAt : DateTime.UtcNow
   })
   .ToListAsync(ct);

      Console.WriteLine($"[MatchService] ? Found {roomMemberships.Count} match(es)");

       if (roomMemberships.Count == 0)
      {
         return Array.Empty<ActiveMatchDto>();
    }

          var matchedUserIds = roomMemberships.Select(m => m.UserId).ToList();

 // 3) Bulk fetch: Last message for each room (separate query)
 var lastMessages = await _db.ChatMessages
    .AsNoTracking()
  .Where(m => userRooms.Contains(m.RoomId))
 .GroupBy(m => m.RoomId)
    .Select(g => new
   {
 RoomId = g.Key,
LastMessageAt = g.Max(m => m.SentAt),
        LastMessage = g.OrderByDescending(m => m.SentAt).Select(m => m.Text).FirstOrDefault()
  })
   .ToListAsync(ct);

       var lastMessageDict = lastMessages.ToDictionary(x => x.RoomId);

    // 4) Bulk fetch: Unread message counts (separate query)
     var unreadCounts = await _db.ChatMessages
       .AsNoTracking()
 .Where(m => userRooms.Contains(m.RoomId) && m.SenderId != userId)
    .GroupBy(m => m.RoomId)
      .Select(g => new { RoomId = g.Key, Count = g.Count() })
          .ToListAsync(ct);

       var unreadCountDict = unreadCounts.ToDictionary(x => x.RoomId, x => x.Count);

// 5) Get current user's liked movie IDs (separate query)
   var myLikes = await _db.UserMovieLikes
  .AsNoTracking()
 .Where(x => x.UserId == userId)
.Select(x => x.TmdbId)
   .ToListAsync(ct);

  // 6) Bulk fetch: Shared movie IDs between current user and matched users (separate query)
   var theirLikes = await _db.UserMovieLikes
   .AsNoTracking()
     .Where(x => matchedUserIds.Contains(x.UserId) && myLikes.Contains(x.TmdbId))
     .ToListAsync(ct);

 var sharedMoviesByUser = theirLikes
  .GroupBy(x => x.UserId)
           .ToDictionary(
    g => g.Key, 
               g => g.Select(x => x.TmdbId).Distinct().ToList()
           );

       // 7) Get unique shared movie IDs across all matches
  var allSharedMovieIds = theirLikes.Select(x => x.TmdbId).Distinct().ToList();

   // 8) Fetch movie details for all shared movies (separate query)
   var movieDetails = await _db.UserMovieLikes
   .AsNoTracking()
  .Where(ml => allSharedMovieIds.Contains(ml.TmdbId))
          .ToListAsync(ct);

       // Build movie dictionary IN MEMORY (not in EF query)
        var imageBaseUrl = "https://image.tmdb.org/t/p/";
    var moviesByTmdbId = movieDetails
        .GroupBy(ml => ml.TmdbId)
           .Select(g => g.First())
      .ToDictionary(
 ml => ml.TmdbId,
 ml => new SharedMovieDto
{
       TmdbId = ml.TmdbId,
        Title = ml.Title ?? "",
     PosterUrl = string.IsNullOrWhiteSpace(ml.PosterPath)
       ? ""
       : $"{imageBaseUrl}w342{ml.PosterPath}",
  ReleaseYear = ml.ReleaseYear
 });

            // 9) Map to DTOs (all in memory now)
    var result = roomMemberships
       .Select(m =>
   {
    var sharedMovieIds = sharedMoviesByUser.TryGetValue(m.UserId, out var ids) 
  ? ids 
               : new List<int>();
             
var sharedMovies = sharedMovieIds
  .Where(tmdbId => moviesByTmdbId.ContainsKey(tmdbId))
    .Select(tmdbId => moviesByTmdbId[tmdbId])
       .ToList();

      lastMessageDict.TryGetValue(m.RoomId, out var lastMsg);
    unreadCountDict.TryGetValue(m.RoomId, out var unread);

       return new ActiveMatchDto
 {
     UserId = m.UserId,
       DisplayName = m.DisplayName,
    RoomId = m.RoomId,
  MatchedAt = m.JoinedAt,
          LastMessageAt = lastMsg?.LastMessageAt,
        LastMessage = lastMsg?.LastMessage,
UnreadCount = unread,
    SharedMovies = sharedMovies
      };
      })
    .OrderByDescending(m => m.LastMessageAt ?? m.MatchedAt)
  .ToList();

  Console.WriteLine($"[MatchService] ? Returning {result.Count} active match(es)");

         return result;
        }

        /// <summary>
      /// Get the current match status between current user and a specific target user.
   /// Useful for profile pages and quick status checks.
        /// </summary>
 public async Task<MatchStatusDto> GetMatchStatusAsync(string userId, string targetUserId, CancellationToken ct)
        {
 Console.WriteLine($"[MatchService] ?? Checking match status between {userId} and {targetUserId}...");

            // 1) Check if they have a chat room together (matched)
 var chatRoom = await _db.ChatMemberships
    .AsNoTracking()
    .Where(m => m.UserId == userId || m.UserId == targetUserId)
    .GroupBy(m => m.RoomId)
    .Where(g => g.Count() == 2 && g.Select(m => m.UserId).Distinct().Count() == 2)
        .Select(g => g.Key)
          .FirstOrDefaultAsync(ct);

  if (chatRoom != Guid.Empty)
     {
   Console.WriteLine($"[MatchService] ? Users are matched (Room: {chatRoom})");

 // Get shared movies
      var matchedSharedMovies = await GetSharedMoviesAsync(userId, targetUserId, ct);

   return new MatchStatusDto
  {
     Status = "matched",
  CanMatch = false,
  CanDecline = false,
        RequestSentAt = null,
  RoomId = chatRoom,
  SharedMovies = matchedSharedMovies
    };
   }

       // 2) Check for match requests
  var sentRequest = await _db.MatchRequests
    .AsNoTracking()
    .FirstOrDefaultAsync(x =>
       x.RequestorId == userId &&
      x.TargetUserId == targetUserId, ct);

          var receivedRequest = await _db.MatchRequests
         .AsNoTracking()
   .FirstOrDefaultAsync(x =>
x.RequestorId == targetUserId &&
    x.TargetUserId == userId, ct);

     string status;
          bool canMatch;
     bool canDecline;
            DateTime? requestSentAt;

  if (sentRequest != null && receivedRequest != null)
            {
         // Both sent requests = effectively matched (edge case)
         Console.WriteLine($"[MatchService] ? Both users sent requests (effectively matched)");
     status = "matched";
          canMatch = false;
     canDecline = false;
   requestSentAt = sentRequest.CreatedAt;
   }
   else if (sentRequest != null)
  {
     // Current user sent request
     Console.WriteLine($"[MatchService] ? User sent request (pending_sent)");
   status = "pending_sent";
      canMatch = false;
     canDecline = false;
 requestSentAt = sentRequest.CreatedAt;
        }
else if (receivedRequest != null)
{
     // Target user sent request
        Console.WriteLine($"[MatchService] ? User received request (pending_received)");
       status = "pending_received";
     canMatch = true; // Can accept the request
   canDecline = true;
    requestSentAt = receivedRequest.CreatedAt;
            }
else
  {
// No requests exist
          Console.WriteLine($"[MatchService] ??  No match requests exist (none)");
     status = "none";
  canMatch = true;
         canDecline = false;
            requestSentAt = null;
}

          // Get shared movies
var sharedMovies = await GetSharedMoviesAsync(userId, targetUserId, ct);

  return new MatchStatusDto
   {
  Status = status,
  CanMatch = canMatch,
    CanDecline = canDecline,
         RequestSentAt = requestSentAt,
   RoomId = null,
       SharedMovies = sharedMovies
 };
        }

   /// <summary>
        /// Helper: Get shared movies between two users
        /// </summary>
  private async Task<List<SharedMovieDto>> GetSharedMoviesAsync(string userId1, string userId2, CancellationToken ct)
   {
    // Get movies both users liked
   var user1Likes = await _db.UserMovieLikes
   .AsNoTracking()
            .Where(x => x.UserId == userId1)
    .Select(x => x.TmdbId)
     .ToListAsync(ct);

    var sharedMovieData = await _db.UserMovieLikes
        .AsNoTracking()
        .Where(x => x.UserId == userId2 && user1Likes.Contains(x.TmdbId))
        .ToListAsync(ct);

    // Build shared movies list IN MEMORY (not in EF query)
    var imageBaseUrl = "https://image.tmdb.org/t/p/";
    var sharedMovies = sharedMovieData
        .GroupBy(x => x.TmdbId)
        .Select(g => g.First())
      .Select(ml => new SharedMovieDto
        {
            TmdbId = ml.TmdbId,
         Title = ml.Title ?? "",
            PosterUrl = string.IsNullOrWhiteSpace(ml.PosterPath)
      ? ""
        : $"{imageBaseUrl}w342{ml.PosterPath}",
            ReleaseYear = ml.ReleaseYear
        })
        .ToList();

   return sharedMovies;
     }

    /// <summary>
   /// Send real-time notification to target user that they received a match request.
  /// Called when User A sends a match request to User B.
      /// </summary>
  private async Task SendMatchRequestReceivedNotificationAsync(string requestorId, string targetUserId, int tmdbId)
{
  try
  {
     // Get requestor's display name
  var requestor = await _db.Users
    .AsNoTracking()
      .Where(u => u.Id == requestorId)
      .Select(u => new { u.Id, u.DisplayName })
      .FirstOrDefaultAsync();

   // Get shared movie details
   var sharedMovie = await _db.UserMovieLikes
.AsNoTracking()
         .Where(l => l.UserId == requestorId && l.TmdbId == tmdbId)
         .Select(l => new { l.Title, l.TmdbId })
  .FirstOrDefaultAsync();

  // Count shared movies between users
    var sharedMoviesCount = await _db.UserMovieLikes
.AsNoTracking()
      .Where(l => l.UserId == targetUserId)
         .Select(l => l.TmdbId)
     .Intersect(_db.UserMovieLikes.AsNoTracking()
      .Where(l => l.UserId == requestorId)
          .Select(l => l.TmdbId))
    .CountAsync();

       // Build notification payload
  var matchData = new
    {
       type = "matchRequestReceived",
   matchId = $"request-{requestorId}-{targetUserId}",
       user = new
{
         id = requestorId,
       displayName = requestor?.DisplayName ?? "Someone"
     },
      sharedMovieTitle = sharedMovie?.Title ?? "a movie you liked",
        sharedMovieTmdbId = sharedMovie?.TmdbId ?? tmdbId,
   sharedMoviesCount = sharedMoviesCount,
         timestamp = DateTime.UtcNow,
      message = $"{requestor?.DisplayName ?? "Someone"} wants to match with you!"
        };

// Send notification via SignalR
      await _notificationService.SendMatchNotificationAsync(targetUserId, matchData);
  Console.WriteLine($"[MatchService] ?? Sent match request notification to {targetUserId}");
  }
  catch (Exception ex)
   {
       // Log error but don't throw (notification failure shouldn't break match creation)
  Console.WriteLine($"[MatchService] ? Failed to send match request notification: {ex.Message}");
        }
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
