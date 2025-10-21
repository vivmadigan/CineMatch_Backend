using Infrastructure.Data.Entities;
using Infrastructure.Models;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SignInController(SignInManager<UserEntity> signInManager, UserManager<UserEntity> userManager, ITokenService tokenService) : ControllerBase
    {
        private readonly SignInManager<UserEntity> _signInManager = signInManager;
        private readonly UserManager<UserEntity> _userManager = userManager;
        private readonly ITokenService _tokenService = tokenService;

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> SignIn(SignInDto model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if(user != null)
                {
                    var signInResult = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: false);
                    if (signInResult.Succeeded)
                    {
                        var token = _tokenService.CreateToken(user);
                        return Ok(new { token, userId = user.Id, email = user.Email, displayName = user.DisplayName });
                    }
                }

            }
            return Unauthorized(new { error = "Invalid email or password"});
        }
    }
}
