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
        /// Discover movies filtered by user preferences or explicit query parameters.
        /// Falls back to saved preferences if genres/length are not provided.
        /// </summary>
        /// <param name="genres">Comma-separated genre IDs (e.g., "28,35" for Action+Comedy). Optional.</param>
        /// <param name="length">Length bucket: "short", "medium", or "long". Optional.</param>
        /// <param name="page">Page number (default 1).</param>
        /// <param name="batchSize">Number of movies to return (default 5).</param>
        /// <param name="language">Language code (e.g., "en-US"). Optional.</param>
        /// <param name="region">Region code (e.g., "US"). Optional.</param>
        /// <param name="ct">Cancellation token</param>
        [HttpGet("discover")]
        [ProducesResponseType(typeof(IEnumerable<MovieSummaryDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Discover(
            [FromServices] IPreferenceService prefs,
        [FromQuery] string? genres = null,
            [FromQuery] string? length = null,
            [FromQuery] int page = 1,
            [FromQuery] int batchSize = 5,
   [FromQuery] string? language = null,
     [FromQuery] string? region = null,
  CancellationToken ct = default)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            // Parse genres from query or fall back to user preferences
            List<int> genreIds;
            string lengthKey;

            if (!string.IsNullOrWhiteSpace(genres) || !string.IsNullOrWhiteSpace(length))
            {
                // Use explicit query parameters
                genreIds = ParseGenres(genres);
                lengthKey = length?.ToLowerInvariant() ?? "medium";
            }
            else
            {
                // Load from saved preferences
                var userPrefs = await prefs.GetAsync(userId, ct);
                genreIds = userPrefs.GenreIds;
                lengthKey = userPrefs.Length?.ToLowerInvariant() ?? "medium";
            }

            // Map length key to runtime bounds
            var (runtimeMin, runtimeMax) = MapLengthToRuntime(lengthKey);

            // Call TMDB with filters
            var resp = await _tmdb.DiscoverAsync(genreIds, runtimeMin, runtimeMax, page, language, region, ct);

            // Map to DTOs and take requested batch size
            var movies = resp.Results
           .Take(Math.Max(1, batchSize))
                .Select(m => new MovieSummaryDto
                {
                    Id = m.Id,
                    Title = m.Title,
                    OneLiner = OneLine(m.Overview),
                    RuntimeMinutes = null,
                    PosterUrl = Img(_opt.ImageBase, m.PosterPath, "w342"),
                    BackdropUrl = Img(_opt.ImageBase, m.BackdropPath, "w780"),
                    GenreIds = m.GenreIds,
                    ReleaseYear = string.IsNullOrEmpty(m.ReleaseDate) ? null : m.ReleaseDate.Split('-')[0],
                    Rating = Math.Round(m.VoteAverage, 1),
                    TmdbUrl = $"https://www.themoviedb.org/movie/{m.Id}"
                })
         .ToList();

            return Ok(movies);
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

        /// <summary>
        /// Like (or re-like) a movie; idempotent upsert.
        /// Stores a snapshot (title, poster, year) for quick display without hitting TMDB.
        /// NO automatic match requests - users must manually click "Match" button.
        /// </summary>
        /// <param name="tmdbId">TMDB movie ID (e.g., 27205 for "The Shawshank Redemption")</param>
        /// <param name="body">Optional movie snapshot: title, poster path, and release year</param>
        /// <param name="likes">User likes service (injected)</param>
        /// <param name="ct">Cancellation token</param>
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

 Console.WriteLine($"\n[MoviesController] ════════════════════════════════════════════════════════");
      Console.WriteLine($"[MoviesController] 💚 User {userId} liking movie {tmdbId}");
        Console.WriteLine($"[MoviesController] 🎬 Movie: {body.Title} ({body.ReleaseYear})");
     Console.WriteLine($"[MoviesController] ════════════════════════════════════════════════════════");

   // Save the like
            await likes.UpsertLikeAsync(userId, tmdbId, body.Title, body.PosterPath, body.ReleaseYear, ct);
      Console.WriteLine($"[MoviesController] ✅ Like saved to database");
       Console.WriteLine($"[MoviesController] ℹ️  NO automatic match requests - user must manually click 'Match' button");
       Console.WriteLine($"[MoviesController] ════════════════════════════════════════════════════════\n");
    
       return NoContent();
        }
        /// <summary>
        /// Remove a like (unlike a movie); idempotent.
        /// Safe to call multiple times for the same movie.
        /// </summary>
        /// <param name="tmdbId">TMDB movie ID (e.g., 27205 for "The Shawshank Redemption")</param>
        /// <param name="likes">User likes service (injected)</param>
        /// <param name="ct">Cancellation token</param>
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

        // Parse comma-separated genre IDs from query string
        private static List<int> ParseGenres(string? genresParam)
        {
            if (string.IsNullOrWhiteSpace(genresParam)) return new List<int>();

            return genresParam
     .Split(',', StringSplitOptions.RemoveEmptyEntries)
    .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
    .Where(id => id > 0)
          .ToList();
        }

        // Map length key to runtime bounds (matches the buckets defined in Options endpoint)
        private static (int? min, int? max) MapLengthToRuntime(string lengthKey)
        {
            return lengthKey switch
            {
                "short" => (null, 99),
                "medium" => (100, 140),
                "long" => (141, null),
                _ => (100, 140) // default to medium
            };
        }
    }
}

