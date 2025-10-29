using Infrastructure.Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    // Purpose: hide EF from controllers; expose simple like/unlike/list ops.
    public interface IUserLikesService
    {
        Task UpsertLikeAsync(string userId, int tmdbId, string? title, string? posterPath, string? releaseYear, CancellationToken ct);
        Task RemoveLikeAsync(string userId, int tmdbId, CancellationToken ct);
        Task<IReadOnlyList<UserMovieLike>> GetLikesAsync(string userId, CancellationToken ct);

        /// <summary>
        /// Get all user IDs who have liked a specific movie (excluding the specified user).
        /// Used for automatic match request creation.
        /// </summary>
        /// <param name="tmdbId">Movie ID to check</param>
        /// <param name="excludeUserId">User ID to exclude from results</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>List of user IDs who liked this movie</returns>
        Task<IReadOnlyList<string>> GetUsersWhoLikedMovieAsync(int tmdbId, string excludeUserId, CancellationToken ct);
    }
}
