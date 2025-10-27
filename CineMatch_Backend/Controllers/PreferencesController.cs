using Infrastructure.External;
using Infrastructure.Options;
using Infrastructure.Preferences;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PreferencesController(IPreferenceService preferenceService, ITmdbClient tmdbClient, IMemoryCache cache) : ControllerBase
    {
        private readonly IPreferenceService _service = preferenceService;
        private readonly ITmdbClient _tmdb = tmdbClient;
        private readonly IMemoryCache _cache = cache;

        [HttpGet]
        [ProducesResponseType(typeof(GetPreferencesDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<GetPreferencesDto>> Get(CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var dto = await _service.GetAsync(userId, ct);
            return Ok(dto);
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Save([FromBody] SavePreferenceDto dto, CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            // ✅ Validate genre IDs exist in TMDB (if any provided)
            if (dto.GenreIds != null && dto.GenreIds.Count > 0)
            {
                var validationResult = await ValidateGenreIdsAsync(dto.GenreIds, ct);
                if (!validationResult.IsValid)
                {
                    return BadRequest(new { error = validationResult.ErrorMessage });
                }
            }

            try
            {
                await _service.SaveAsync(userId, dto, ct);
                return NoContent();
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex) // Catches ArgumentNullException too
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Validates that all provided genre IDs exist in TMDB.
        /// Uses cached genre list for performance (24-hour cache).
        /// </summary>
        private async Task<(bool IsValid, string? ErrorMessage)> ValidateGenreIdsAsync(List<int> genreIds, CancellationToken ct)
        {
            // ✅ Use different cache key to avoid conflict with MoviesController
            const string cacheKey = "tmdb_genre_ids:en-US";

            // Get valid genre IDs from cache or TMDB
            var validGenreIds = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);

                try
                {
                    var genreResponse = await _tmdb.GetGenresAsync("en-US", ct);
                    return genreResponse.Genres.Select(g => g.Id).ToHashSet();
                }
                catch
                {
                    // If TMDB is down, cache for shorter time and allow the request
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                    return new HashSet<int>(); // Empty set means skip validation
                }
            });

            // If we couldn't get genres from TMDB (API down), skip validation
            if (validGenreIds == null || validGenreIds.Count == 0)
            {
                return (true, null); // Allow request to proceed
            }

            // Check for invalid genre IDs
            var invalidGenreIds = genreIds.Where(id => !validGenreIds.Contains(id)).ToList();

            if (invalidGenreIds.Any())
            {
                return (false, $"Invalid genre IDs: {string.Join(", ", invalidGenreIds)}. Please select from valid TMDB genres.");
            }

            return (true, null);
        }
    }
}

