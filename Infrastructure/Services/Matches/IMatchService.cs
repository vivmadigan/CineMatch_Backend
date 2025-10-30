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
        /// Decline a match request from another user.
        /// Removes the incoming match request and optionally notifies the original requestor.
        /// </summary>
        /// <param name="declinerUserId">User who is declining the request</param>
        /// <param name="requestorUserId">User who originally sent the request</param>
        /// <param name="tmdbId">Movie ID associated with the request</param>
        /// <param name="ct">Cancellation token</param>
        Task DeclineMatchAsync(string declinerUserId, string requestorUserId, int tmdbId, CancellationToken ct);

        /// <summary>
        /// Get all active matches for a user (users they have chat rooms with).
        /// Includes last message preview, unread count, and shared movies.
        /// Used for "Active Matches" or "Chats" page.
        /// </summary>
        /// <param name="userId">Current user's ID</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>List of active matches with chat preview data</returns>
        Task<IReadOnlyList<ActiveMatchDto>> GetActiveMatchesAsync(string userId, CancellationToken ct);

        /// <summary>
        /// Get the current match status between current user and a specific target user.
        /// Useful for profile pages and quick status checks without loading full candidates list.
        /// </summary>
        /// <param name="userId">Current user's ID</param>
        /// <param name="targetUserId">Target user's ID to check status with</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Match status details including can/cannot actions</returns>
        Task<MatchStatusDto> GetMatchStatusAsync(string userId, string targetUserId, CancellationToken ct);
    }
}
