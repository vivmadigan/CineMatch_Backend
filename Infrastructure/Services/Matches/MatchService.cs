using Infrastructure.Data.Context;
using Infrastructure.Data.Entities;
using Infrastructure.Models.Matches;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services.Matches
{
    public sealed class MatchService : IMatchService
 {
     private readonly ApplicationDbContext _db;

        public MatchService(ApplicationDbContext db) => _db = db;

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
//    Group by UserId, calculate overlap count, get latest like timestamp, collect shared movie IDs
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
     }

    // Return no match (request saved or already existed)
    return new MatchResultDto
 {
         Matched = false,
  RoomId = null
    };
        }
  }
}
