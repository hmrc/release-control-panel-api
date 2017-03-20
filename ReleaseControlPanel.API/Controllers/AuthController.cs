using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseControlPanel.API.Models;
using ReleaseControlPanel.API.Repositories;

namespace ReleaseControlPanel.API.Controllers
{
    [Route("[controller]")]
    public class AuthController : Controller
    {
        private ILogger _logger;
        private IUserRepository _userRepository;
        private AuthSettings _settings;

        public AuthController(ILogger<AuthController> logger, IUserRepository userRepository, IOptions<AuthSettings> settings)
        {
            _logger = logger;
            _userRepository = userRepository;
            _settings = settings.Value;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginData loginData)
        {
            if (string.IsNullOrEmpty(loginData?.UserName))
            {
                _logger.LogWarning("Cannot login user with empty or null UserName.");
                return BadRequest("UserName must not be empty.");
            }

            if (string.IsNullOrEmpty(loginData.Password))
            {
                _logger.LogWarning("Cannot login user with empty or null Password.");
                return BadRequest("Password must not be empty.");
            }

            var user = await _userRepository.FindByUserName(loginData.UserName);
            if (user == null || !user.VerifyPassword(loginData.Password))
            {
                _logger.LogTrace($"Invalid user name or password for user '{loginData.UserName}'.");
                return Unauthorized();
            }

            user.DecryptUserSecret(loginData.Password);
            user.DecryptData(user.UserSecret);

            var claims = new []
            {
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim("UserSecret", user.UserSecret) 
            };
            var identity = new ClaimsIdentity(claims, "Basic");
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.Authentication.SignInAsync(_settings.AuthenticationScheme, principal);

            user.ClearSensitiveData();

            return Json(user);
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.Authentication.SignOutAsync(_settings.AuthenticationScheme);
            return StatusCode(204);
        }
    }
}