using Infrastructure.External;
using Infrastructure.Likes;
using Infrastructure.Models;
using Infrastructure.Options;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Security.Claims;

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
            [FromQuery] int page = 1,
            [FromQuery] string? language = null,
            [FromQuery] string? region = null,
            CancellationToken ct = default)
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
        [ProducesResponseType(typeof(MovieOptionsDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Options(
            [FromServices] IMemoryCache cache,
            [FromQuery] string? language = null,
            CancellationToken ct = default)
        {
            // App-defined buckets (no DB needed for MVP)
            var lengths = new List<LengthOptionDto>
    {
        new() { Key = "short",  Label = "Short (<100 min)",  Min = null, Max = 99 },
        new() { Key = "medium", Label = "Medium (100–140)",  Min = 100,  Max = 140 },
        new() { Key = "long",   Label = "Long (>140 min)",   Min = 141,  Max = null },
    };

            var lang = string.IsNullOrWhiteSpace(language) ? _opt.DefaultLanguage : language!;
            var cacheKey = $"tmdb_genres:{lang}";

            var genres = await cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);

                var resp = await _tmdb.GetGenresAsync(lang, ct);
                if (resp.Genres.Count == 0)
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1); // avoid caching empty for a day

                // Strongly-typed so Swagger shows a real schema
                return resp.Genres
                           .OrderBy(g => g.Name)
                           .Select(g => new GenreOptionsDto { Id = g.Id, Name = g.Name })
                           .ToList();
            });

            return Ok(new MovieOptionsDto { Lengths = lengths, Genres = genres ?? new List<GenreOptionsDto>() });
        }
        /// <summary>Return the current user's liked movies (most recent first).</summary>
        [HttpGet("likes")]
        [ProducesResponseType(typeof(IEnumerable<MovieLikeDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLikes([FromServices] IUserLikesService likes, CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var rows = await likes.GetLikesAsync(userId, ct);

            // Build full poster URL from stored PosterPath
            string? FullPoster(string? path) =>
                string.IsNullOrWhiteSpace(path) ? null : $"{_opt.ImageBase.TrimEnd('/')}/w342{path}";

            var list = rows.Select(x => new MovieLikeDto
            {
                TmdbId = x.TmdbId,
                Title = x.Title,
                PosterUrl = FullPoster(x.PosterPath),
                ReleaseYear = x.ReleaseYear,
                LikedAt = x.CreatedAt
            });

            return Ok(list);
        }

        /// <summary>Like (or re-like) a movie; idempotent upsert. Provide an optional snapshot.</summary>
        [HttpPost("{tmdbId:int}/like")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Like(
            int tmdbId,
            [FromBody] LikeMovieRequestDto body,
            [FromServices] IUserLikesService likes,
            CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            await likes.UpsertLikeAsync(userId, tmdbId, body.Title, body.PosterPath, body.ReleaseYear, ct);
            return NoContent();
        }

        /// <summary>Remove a like (idempotent).</summary>
        [HttpDelete("{tmdbId:int}/like")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Unlike(
            int tmdbId,
            [FromServices] IUserLikesService likes,
            CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            await likes.RemoveLikeAsync(userId, tmdbId, ct);
            return NoContent();
        }
    }

}

