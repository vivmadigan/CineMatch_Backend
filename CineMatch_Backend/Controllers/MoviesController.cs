using Infrastructure.External;
using Infrastructure.Models;
using Infrastructure.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    }

}

