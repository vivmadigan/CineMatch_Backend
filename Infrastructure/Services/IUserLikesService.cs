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
    }
}
