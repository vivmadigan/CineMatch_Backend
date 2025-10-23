using Infrastructure.Data.Context;
using Infrastructure.Data.Entities;
using Infrastructure.Preferences;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class PreferenceService : IPreferenceService
    {
        private static readonly HashSet<string> AllowedLengthKeys = new(StringComparer.OrdinalIgnoreCase)
        { "short", "medium", "long" };

        private readonly ApplicationDbContext _db;

        // DI constructor: the framework injects an ApplicationDbContext (scoped per request),
        // and we store it in _db so service methods can query/save without the controller touching EF.
        public PreferenceService(ApplicationDbContext db) => _db = db;

        public async Task<GetPreferencesDto> GetAsync(string userId, CancellationToken ct)
        {
            var pref = await _db.UserPreferences.AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId, ct);

            return new GetPreferencesDto
            {
                GenreIds = pref?.GenreIds ?? new List<int>(),
                Length = pref?.LengthKey ?? "medium"
            };
        }

        public async Task SaveAsync(string userId, SavePreferenceDto dto, CancellationToken ct)
        {
            var key = (dto.Length ?? "").ToLowerInvariant();
            if (!AllowedLengthKeys.Contains(key))
                throw new ArgumentOutOfRangeException(nameof(dto.Length), "length must be short | medium | long");

            var pref = await _db.UserPreferences.FirstOrDefaultAsync(p => p.UserId == userId, ct);
            if (pref is null)
            {
                pref = new UserPreference { UserId = userId };
                _db.UserPreferences.Add(pref);
            }

            pref.GenreIds = dto.GenreIds.Distinct().ToList(); // keep it tidy
            pref.LengthKey = key;
            pref.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
        }
    }
}
