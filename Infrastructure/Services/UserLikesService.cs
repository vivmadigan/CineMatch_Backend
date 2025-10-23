using Infrastructure.Data.Context;
using Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class UserLikesService : IUserLikesService
    {
        private readonly ApplicationDbContext _db;

        // DI constructor: receive scoped DbContext; keep it for operations.
        public UserLikesService(ApplicationDbContext db) => _db = db;

        public async Task UpsertLikeAsync(string userId, int tmdbId, string? title, string? posterPath, string? releaseYear, CancellationToken ct)
        {
            var row = await _db.UserMovieLikes.FirstOrDefaultAsync(x => x.UserId == userId && x.TmdbId == tmdbId, ct);
            if (row is null)
            {
                row = new UserMovieLike { UserId = userId, TmdbId = tmdbId, CreatedAt = DateTime.UtcNow };
                _db.UserMovieLikes.Add(row);
            }

            // Update snapshot each time (keeps latest name/poster/year)
            row.Liked = true;
            row.Title = title ?? row.Title;
            row.PosterPath = posterPath ?? row.PosterPath;
            row.ReleaseYear = releaseYear ?? row.ReleaseYear;

            await _db.SaveChangesAsync(ct);
        }

        public async Task RemoveLikeAsync(string userId, int tmdbId, CancellationToken ct)
        {
            var row = await _db.UserMovieLikes.FirstOrDefaultAsync(x => x.UserId == userId && x.TmdbId == tmdbId, ct);
            if (row is null) return; // idempotent
            _db.UserMovieLikes.Remove(row);
            await _db.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<UserMovieLike>> GetLikesAsync(string userId, CancellationToken ct)
        {
            return await _db.UserMovieLikes.AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(ct);
        }

    }
}
