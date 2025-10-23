using Infrastructure.External;
using Infrastructure.Models;
using Infrastructure.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [Produces("application/json")]
    public class MoviesController : ControllerBase
    {
        private readonly ITmdbClient _tmdb;
        private readonly TmdbOptions _opt;
        public MoviesController(
            ITmdbClient tmdb,
            IOptions<TmdbOptions> opt)
        {
            _tmdb = tmdb;
            _opt = opt.Value;
        }

        /// <summary>
        /// Return 5 popular movies mapped to the app's movie summary format.
        /// This is a test endpoint for MVP demo purposes to make sure api is working.
        /// </summary>
        [HttpGet("test")]
        [ProducesResponseType(typeof(IEnumerable<MovieSummaryDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetFive(
            CancellationToken ct,
            [FromQuery] int page = 1,
            [FromQuery] string? language = null,
            [FromQuery] string? region = null)
        {
            // 1) Get one discover page from TMDB.
            var resp = await _tmdb.DiscoverTopAsync(page, language, region, ct);

            // 2) Map and take 5. We also shorten overviews to be card-friendly.
            var five = resp.Results
                .Take(5)
                .Select(m => new MovieSummaryDto
                {
                    Id = m.Id,
                    Title = m.Title,
                    OneLiner = OneLine(m.Overview),
                    RuntimeMinutes = null, // details not fetched in this MVP
                    PosterUrl = Img(_opt.ImageBase, m.PosterPath, "w342"),
                    BackdropUrl = Img(_opt.ImageBase, m.BackdropPath, "w780"),
                    GenreIds = m.GenreIds,
                    ReleaseYear = string.IsNullOrEmpty(m.ReleaseDate) ? null : m.ReleaseDate.Split('-')[0],
                    Rating = Math.Round(m.VoteAverage, 1),
                    TmdbUrl = $"https://www.themoviedb.org/movie/{m.Id}"
                })
                .ToList();

            return Ok(five);
        }

        // Keep synopses concise for UI cards.
        private static string OneLine(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            s = s.Trim().Replace("\n", " ").Replace("\r", " ");
            return s.Length <= 140 ? s : s[..140].TrimEnd() + "...";
        }

        // Build full CDN URLs so the frontend doesn't need to know TMDB size rules.
        private static string? Img(string baseUrl, string? path, string size) =>
            string.IsNullOrEmpty(path) ? null : $"{baseUrl}{size}{path}";

        /// <summary>
        /// UI options: app-defined length buckets + TMDB genres (cached 24h per language).
        /// </summary>
        [HttpGet("options")]
        public async Task<IActionResult> Options(
            [FromServices] IMemoryCache cache,
            CancellationToken ct,
            [FromQuery] string? language = null)
        {
            // App-defined buckets (no DB needed for MVP)
            var lengths = new List<LengthOptionDto>
            {
                new() { Key = "short",  Label = "Short (<100 min)", Min = null, Max = 99 },
                new() { Key = "medium", Label = "Medium (100–140)", Min = 100,  Max = 140 },
                new() { Key = "long",   Label = "Long (>140 min)",  Min = 141,  Max = null },
            };

            // Cache TMDB genres by language for 24h
            var lang = string.IsNullOrWhiteSpace(language) ? _opt.DefaultLanguage : language!;
            var cacheKey = $"tmdb_genres:{lang}";

            var genres = await cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);

                var resp = await _tmdb.GetGenresAsync(lang, ct);

                // If TMDB fails and returns 0, avoid poisoning the cache for a full day
                if (resp.Genres.Count == 0)
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);

                return resp.Genres
                           .OrderBy(g => g.Name)
                           .Select(g => new { id = g.Id, name = g.Name })
                           .Cast<object>()
                           .ToList();
            });

            return Ok(new MovieOptionsDto { Lengths = lengths, Genres = genres ?? [] });
        }
    }

}

