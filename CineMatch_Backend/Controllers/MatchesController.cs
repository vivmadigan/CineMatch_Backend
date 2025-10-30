using Infrastructure.Models.Matches;
using Infrastructure.Services.Matches;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

            // ? Validate TargetUserId is not empty
            if (string.IsNullOrWhiteSpace(request.TargetUserId))
                return BadRequest(new { error = "TargetUserId is required" });

            // ? Validate TargetUserId is a valid GUID
            if (!Guid.TryParse(request.TargetUserId, out var targetGuid) || targetGuid == Guid.Empty)
                return BadRequest(new { error = "TargetUserId must be a valid non-empty GUID" });

            // ? Validate TmdbId is positive
            if (request.TmdbId <= 0)
                return BadRequest(new { error = "TmdbId must be a positive integer" });

            try
            {
                var result = await matches.RequestAsync(userId, request.TargetUserId, request.TmdbId, ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("User not found"))
            {
                // ? Handle non-existent target user gracefully
                return NotFound(new { error = ex.Message });
            }
            catch (DbUpdateException)
            {
                // ? Handle foreign key constraint violations
                return BadRequest(new { error = "Target user does not exist" });
            }
        }

        /// <summary>
        /// Decline a match request from another user.
        /// Removes the incoming match request and returns 204 No Content.
        /// </summary>
        /// <param name="matches">Match service (injected)</param>
        /// <param name="request">Match decline details (targetUserId is the person who sent the request, tmdbId is the movie)</param>
        /// <param name="ct">Cancellation token</param>
        /// <remarks>
        /// Example request:
        /// 
        ///     POST /api/matches/decline
        ///     {
        ///       "targetUserId": "8bd1e3b8-8f30-4a9f-9b0e-8a8c6e2c0d71",
        ///       "tmdbId": 27205
        ///  }
        ///     
        /// This will decline the match request from the user with ID "8bd1e3b8..." for movie 27205.
        /// </remarks>
        /// <response code="204">Match request declined successfully</response>
        /// <response code="400">Invalid request (missing targetUserId or tmdbId)</response>
        /// <response code="401">User not authenticated</response>
        [HttpPost("decline")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> DeclineMatch(
            [FromServices] IMatchService matches,
            [FromBody] RequestMatchDto request,
            CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            // Validate TargetUserId is not empty
            if (string.IsNullOrWhiteSpace(request.TargetUserId))
                return BadRequest(new { error = "TargetUserId is required" });

            // Validate TargetUserId is a valid GUID
            if (!Guid.TryParse(request.TargetUserId, out var targetGuid) || targetGuid == Guid.Empty)
                return BadRequest(new { error = "TargetUserId must be a valid non-empty GUID" });

            // Validate TmdbId is positive
            if (request.TmdbId <= 0)
                return BadRequest(new { error = "TmdbId must be a positive integer" });

            // Call DeclineMatchAsync: userId is the decliner, targetUserId is the original requestor
            await matches.DeclineMatchAsync(userId, request.TargetUserId, request.TmdbId, ct);

            return NoContent();
        }

        /// <summary>
        /// Get all active matches for the current user.
        /// Returns users you've matched with (have chat rooms) including last message preview.
        /// </summary>
        /// <param name="matches">Match service (injected)</param>
        /// <param name="ct">Cancellation token</param>
        /// <remarks>
        /// Example response:
        /// 
        ///     [
        ///       {
        ///     "userId": "abc-123",
        ///      "displayName": "Alex",
        ///    "roomId": "room-456",
        ///    "matchedAt": "2025-01-31T10:00:00Z",
        ///   "lastMessageAt": "2025-01-31T12:30:00Z",
        /// "lastMessage": "Hey! Want to watch Inception tonight?",
        ///         "unreadCount": 2,
        ///  "sharedMovies": [...]
        ///       }
        ///     ]
        /// </remarks>
        /// <response code="200">List of active matches</response>
        /// <response code="401">User not authenticated</response>
        [HttpGet("active")]
        [ProducesResponseType(typeof(IEnumerable<ActiveMatchDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetActiveMatches(
            [FromServices] IMatchService matches,
            CancellationToken ct = default)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var activeMatches = await matches.GetActiveMatchesAsync(userId, ct);
            return Ok(activeMatches);
        }

        /// <summary>
        /// Get the current match status with a specific user.
        /// Useful for profile pages and quick status checks.
        /// </summary>
        /// <param name="matches">Match service (injected)</param>
        /// <param name="targetUserId">User ID to check status with</param>
        /// <param name="ct">Cancellation token</param>
        /// <remarks>
        /// Example response:
        /// 
        ///     {
        ///       "status": "pending_sent",
        ///       "canMatch": false,
        ///       "canDecline": false,
        ///       "requestSentAt": "2025-01-31T10:00:00Z",
        ///       "roomId": null,
        ///       "sharedMovies": [...]
        ///     }
        /// </remarks>
        /// <response code="200">Match status details</response>
        /// <response code="400">Invalid targetUserId</response>
        /// <response code="401">User not authenticated</response>
        [HttpGet("status/{targetUserId}")]
        [ProducesResponseType(typeof(MatchStatusDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMatchStatus(
            [FromServices] IMatchService matches,
            string targetUserId,
            CancellationToken ct = default)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            // Validate targetUserId
            if (string.IsNullOrWhiteSpace(targetUserId))
                return BadRequest(new { error = "TargetUserId is required" });

            if (!Guid.TryParse(targetUserId, out var targetGuid) || targetGuid == Guid.Empty)
                return BadRequest(new { error = "TargetUserId must be a valid non-empty GUID" });

            // Prevent checking status with self
            if (userId == targetUserId)
                return BadRequest(new { error = "Cannot check match status with yourself" });

            var status = await matches.GetMatchStatusAsync(userId, targetUserId, ct);
            return Ok(status);
        }
    }
}
