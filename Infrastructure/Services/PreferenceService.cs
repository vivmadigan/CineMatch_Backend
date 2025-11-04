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

        private const int MaxGenreCount = 50; // Reasonable limit for UI/UX

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
            // ✅ Validate null DTO
            if (dto == null)
                throw new ArgumentNullException(nameof(dto), "Preferences data cannot be null");

            // ✅ Validate genre IDs are positive
            if (dto.GenreIds.Any(id => id <= 0))
                throw new ArgumentException("Genre IDs must be positive integers", nameof(dto.GenreIds));

            // ✅ Validate genre list size
            if (dto.GenreIds.Count > MaxGenreCount)
                throw new ArgumentException($"Cannot select more than {MaxGenreCount} genres", nameof(dto.GenreIds));

            var key = (dto.Length ?? "").ToLowerInvariant();
            if (!AllowedLengthKeys.Contains(key))
                throw new ArgumentOutOfRangeException(nameof(dto.Length), "length must be short | medium | long");

            // 🔒 RACE CONDITION PROTECTION: Retry on unique constraint violation
            const int maxRetries = 3;
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
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
                    return; // Success - exit
                }
                catch (DbUpdateException ex) when (
                    ex.InnerException?.Message.Contains("UNIQUE constraint failed") == true &&
                    attempt < maxRetries - 1)
                {
                    // Race condition detected: Another request created preferences
                    // Detach the entity and retry with updated data
                    _db.ChangeTracker.Clear();
                    Console.WriteLine($"[PreferenceService] ℹ️  Unique constraint violation on attempt {attempt + 1}, retrying...");
                    await Task.Delay(50 * (attempt + 1), ct); // Exponential backoff: 50ms, 100ms, 150ms
                }
            }

            // If we get here after all retries, throw the original exception
            throw new InvalidOperationException($"Failed to save preferences after {maxRetries} attempts due to concurrent modifications");
        }

        public async Task DeleteAsync(string userId, CancellationToken ct)
        {
            var pref = await _db.UserPreferences.FirstOrDefaultAsync(p => p.UserId == userId, ct);
            if (pref is null) return; // Idempotent - nothing to delete

            _db.UserPreferences.Remove(pref);
            await _db.SaveChangesAsync(ct);
        }
    }
}
