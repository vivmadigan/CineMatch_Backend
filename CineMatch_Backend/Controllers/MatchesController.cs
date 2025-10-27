using Infrastructure.Models.Matches;
using Infrastructure.Services.Matches;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Presentation.Controllers
{
    /// <summary>
    /// Social matching endpoints for finding compatible users and creating chat connections.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [Produces("application/json")]
    public class MatchesController : ControllerBase
    {
        /// <summary>
        /// Get match candidates: users who share movie likes with the current user.
        /// Ordered by overlap count (DESC), then recency (DESC).
        /// </summary>
        /// <param name="matches">Match service (injected)</param>
        /// <param name="take">Max number of candidates to return (default 20)</param>
        /// <param name="ct">Cancellation token</param>
        [HttpGet("candidates")]
        [ProducesResponseType(typeof(IEnumerable<CandidateDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCandidates(
            [FromServices] IMatchService matches,
            [FromQuery] int take = 20,
            CancellationToken ct = default)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var candidates = await matches.GetCandidatesAsync(userId, take, ct);
            return Ok(candidates);
        }

        /// <summary>
        /// Request a match with another user for a specific movie.
        /// If the target user has already requested you for the same movie, a chat room is created.
        /// </summary>
        /// <param name="matches">Match service (injected)</param>
        /// <param name="request">Match request details (targetUserId and tmdbId)</param>
        /// <param name="ct">Cancellation token</param>
        /// <remarks>
        /// Example request:
        /// 
        ///     POST /api/matches/request
        /// {
        ///       "targetUserId": "8bd1e3b8-8f30-4a9f-9b0e-8a8c6e2c0d71",
        ///       "tmdbId": 27205
        ///     }
        ///     
        /// Example response when first user requests (no mutual match yet):
        /// 
        ///     {
        ///       "matched": false,
        ///    "roomId": null
        ///     }
        ///     
        /// Example response when second user requests (mutual match):
        /// 
        ///     {
        ///  "matched": true,
        ///       "roomId": "c5a5a0a4-5e2d-4a6a-9b7b-7c6d3d1e2f90"
        ///   }
        /// </remarks>
        /// <response code="200">Match request processed; check 'matched' field for result</response>
        /// <response code="400">Invalid request (missing targetUserId or tmdbId)</response>
        /// <response code="401">User not authenticated</response>
        [HttpPost("request")]
        [ProducesResponseType(typeof(MatchResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RequestMatch(
            [FromServices] IMatchService matches,
            [FromBody] RequestMatchDto request,
            CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            // Prevent self-matching
            if (userId == request.TargetUserId)
                return BadRequest(new { error = "Cannot match with yourself" });

            var result = await matches.RequestAsync(userId, request.TargetUserId, request.TmdbId, ct);
            return Ok(result);
        }
    }
}
