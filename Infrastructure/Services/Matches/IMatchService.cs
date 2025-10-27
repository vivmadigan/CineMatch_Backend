using Infrastructure.Models.Matches;

namespace Infrastructure.Services.Matches
{
    // Purpose: Find users with overlapping movie likes for potential matching.
    // Why: Encapsulates matching logic; keeps controllers thin and testable.
 public interface IMatchService
{
    /// <summary>
        /// Get match candidates for the given user, ranked by overlap count and recency.
   /// </summary>
        /// <param name="userId">Current user's ID</param>
   /// <param name="take">Max number of candidates to return (default 20)</param>
        /// <param name="ct">Cancellation token</param>
    /// <returns>List of candidates with overlap details</returns>
     Task<IReadOnlyList<CandidateDto>> GetCandidatesAsync(string userId, int take, CancellationToken ct);

   /// <summary>
 /// Request a match with another user for a specific movie.
     /// If reciprocal request exists, creates a chat room and returns matched=true.
   /// Otherwise, saves the request and returns matched=false.
   /// </summary>
     /// <param name="requestorId">User making the request</param>
 /// <param name="targetUserId">User being requested</param>
     /// <param name="tmdbId">TMDB movie ID (e.g., 27205)</param>
     /// <param name="ct">Cancellation token</param>
        /// <returns>Match result indicating if room was created</returns>
  Task<MatchResultDto> RequestAsync(string requestorId, string targetUserId, int tmdbId, CancellationToken ct);
    }
}
