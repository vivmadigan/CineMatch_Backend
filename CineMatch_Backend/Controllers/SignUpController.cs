using Infrastructure.Data.Entities;
using Infrastructure.Models;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SignUpController(UserManager<UserEntity> userManager, ITokenService tokenService) : ControllerBase
    {
        private readonly UserManager<UserEntity> _userManager = userManager;
        private readonly ITokenService _tokenService = tokenService;

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> SignUp(SignUpDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(model);

            // Check if email is already in the system - quick check
            if (await _userManager.Users.AnyAsync(u => u.Email == model.Email))
            {
                return Conflict(new { error = "Email is already in use." });
            }

            if (await _userManager.Users.AnyAsync(dn => dn.DisplayName == model.DisplayName))
            {
                return Conflict(new { error = "Display Name is already in use." });
            }

            // Map SignUpDto to UserEntity
            var user = new UserEntity
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                DisplayName = model.DisplayName
            };

            try
            {
                var result = await _userManager.CreateAsync(user, model.Password);
                if (!result.Succeeded)
                    return BadRequest(result.Errors);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An unexpected error occurred during sign up.", details = ex.Message });
            }

            return Ok(new AuthResponseDto
            {
                Token = _tokenService.CreateToken(user),
                UserId = user.Id,
                Email = user.Email!,
                DisplayName = user.DisplayName!
            });
        }
    }
}
