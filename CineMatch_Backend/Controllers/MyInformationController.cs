using Infrastructure.Data.Entities;
using Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MyInformationController(UserManager<UserEntity> userManager) : ControllerBase
    {
        private readonly UserManager<UserEntity> _userManager = userManager;

        [Authorize]
        [HttpGet]
        [ProducesResponseType(typeof(MyInformationDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyInformation()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
                return NotFound();

            return Ok(new MyInformationDto
            {
                UserId = user.Id,
                Email = user.Email ?? "",
                FirstName = user.FirstName ?? "",
                LastName = user.LastName ?? "",
                DisplayName = user.DisplayName ?? ""
            });
        }
    }
}
