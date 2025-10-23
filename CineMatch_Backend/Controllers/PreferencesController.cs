using Infrastructure.Preferences;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PreferencesController(IPreferenceService preferenceService) : ControllerBase
    {
        private readonly IPreferenceService _service = preferenceService;

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

            try
            {
                await _service.SaveAsync(userId, dto, ct);
                return NoContent();
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}

