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

        /// <summary>
        /// Automatically create ONE-WAY match requests when a user likes a movie.
        /// Finds all other users who liked the same movie and creates requests FROM current user TO them.
        /// Other users will see these requests and can manually accept/decline.
        /// Runs synchronously to ensure requests are created before API response.
        /// </summary>
        /// <param name="userId">User who just liked the movie</param>
        /// <param name="tmdbId">Movie ID that was liked</param>
        /// <param name="ct">Cancellation token</param>
        Task CreateAutoMatchRequestsAsync(string userId, int tmdbId, CancellationToken ct);

        /// <summary>
        /// Decline a match request from another user.
        /// Removes the incoming match request and optionally notifies the original requestor.
        /// </summary>
        /// <param name="declinerUserId">User who is declining the request</param>
        /// <param name="requestorUserId">User who originally sent the request</param>
        /// <param name="tmdbId">Movie ID associated with the request</param>
        /// <param name="ct">Cancellation token</param>
        Task DeclineMatchAsync(string declinerUserId, string requestorUserId, int tmdbId, CancellationToken ct);
    }
}
