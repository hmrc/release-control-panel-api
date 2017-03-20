using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ReleaseControlPanel.API.Models;
using ReleaseControlPanel.API.Repositories;

namespace ReleaseControlPanel.API.Controllers
{
    [Authorize]
    [Route("[controller]")]
    public class UserController : Controller
    {
        private readonly ILogger _logger;
        private readonly IUserRepository _userRepository;

        public UserController(ILogger<UserController> logger, IUserRepository userRepository)
        {
            _logger = logger;
            _userRepository = userRepository;
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordData data)
        {
            if (data == null)
            {
                _logger.LogError("Tried to change password with data = null.");
                return BadRequest("You must provide both old and new password.");
            }

            if (string.IsNullOrEmpty(data.OldPassword))
            {
                _logger.LogError("Tried to change password with old password set to null.");
                return BadRequest("Cannot change password with old password set to null.");
            }

            if (string.IsNullOrEmpty(data.NewPassword))
            {
                _logger.LogError("Tried to change password with new password set to null.");
                return BadRequest("Cannot change password with new password set to null.");
            }

            var currentUser = await _userRepository.FindByUserName(User.Identity.Name);
            if (!currentUser.VerifyPassword(data.OldPassword))
            {
                _logger.LogTrace($"Cannot change password: User '{User.Identity.Name}' provided incorrect old password.");
                return BadRequest("Old password is incorrect");
            }

            currentUser.DecryptUserSecret(data.OldPassword);

            var userSecret = currentUser.UserSecret;
            currentUser.DecryptData(userSecret);

            currentUser.SetPassword(data.NewPassword);

            currentUser.EncryptData(userSecret);

            await _userRepository.Update(currentUser);

            return NoContent();
        }

        [HttpPut]
        public async Task<IActionResult> CreateUser([FromBody] User user)
        {
            if (user == null)
            {
                _logger.LogWarning("Tried to create a 'null' user.");
                return BadRequest("User cannot be null.");
            }

            var existingUser = await _userRepository.FindByUserName(user.UserName);
            if (existingUser != null)
            {
                _logger.LogTrace($"Cannot create a new user: User with UserName = '{user.UserName}' already exists.");
                return StatusCode(409, "User with this UserName already exists.");
            }

            user.Admin = false;
            user.GenerateUserSecret();
            user.EncryptData(user.UserSecret);
            user.SetPassword(user.Password);

            await _userRepository.Insert(user);

            return CreatedAtAction("GetUser", new { userName = user.UserName }, null);
        }

        [HttpDelete("{userName}")]
        public async Task<IActionResult> DeleteUser(string userName)
        {
            if (string.IsNullOrEmpty(userName))
            {
                _logger.LogWarning("Tried to delete user with UserName = 'null'.");
                return BadRequest("UserName cannot be null.");
            }

            var existingUser = await _userRepository.FindByUserName(userName);
            if (existingUser == null)
            {
                _logger.LogTrace($"Cannot delete user: User with UserName = '{userName}' does not exist.");
                return NotFound("User with this user name does not exist.");
            }

            var deleteResult = await _userRepository.Delete(existingUser.Id.ToString());
            if (deleteResult.DeletedCount != 1)
            {
                _logger.LogError($"Could not delete user with UserName = '{userName}'. MongoDB client returned DeleteCound = {deleteResult.DeletedCount}.");
                return StatusCode(500,
                    $"Could not delete the user with UserName = '{userName}'. Check server logs for more information.");
            }

            return NoContent();
        }

        [HttpGet]
        public async Task<IActionResult> GetCurrentUser()
        {
            var user = await _userRepository.FindByUserName(User.Identity.Name);
            if (user == null)
            {
                _logger.LogCritical("Failing to get logged in user is not correctly supported yet!");
                throw new NotImplementedException();
            }

            user.DecryptData(User.FindFirst("UserSecret").Value);

            user.ClearSensitiveData();

            return Json(user);
        }

        [HttpGet("{userName}")]
        public async Task<IActionResult> GetUser(string userName)
        {
            if (string.IsNullOrEmpty(userName))
            {
                _logger.LogWarning("Tried to get user with UserName = 'null'.");
                return BadRequest("UserName cannot be null.");
            }

            var user = await _userRepository.FindByUserName(userName);
            if (user == null)
            {
                _logger.LogTrace($"Cannot find user with UserName = '{userName}'.");
                return NotFound("Cannot find user with this UserName.");
            }

            user.ClearSensitiveData();

            return Json(user);
        }

        [HttpPost]
        public async Task<IActionResult> SaveCurrentUser([FromBody] User user)
        {
            if (user == null)
            {
                _logger.LogWarning("Tried to save empty user.");
                return BadRequest("User cannot be null. Use DELETE to remove the user if that's what you wanted.");
            }

            if (User.Identity.Name != user.UserName)
            {
                _logger.LogWarning("Cannot change UserName of the user!");
                return BadRequest("'User.UserName' cannot be different from current user name.");
            }

            var userSecret = User.FindFirst("UserSecret").Value;
            var dbUser = await _userRepository.FindByUserName(user.UserName);
            dbUser.DecryptData(userSecret);

            dbUser.CiBuildApiToken = user.CiBuildApiToken;
            dbUser.CiBuildUserName = user.CiBuildUserName;
            dbUser.CiQaApiToken = user.CiQaApiToken;
            dbUser.CiQaUserName = user.CiQaUserName;
            dbUser.CiStagingApiToken = user.CiStagingApiToken;
            dbUser.CiStagingUserName = user.CiStagingUserName;
            dbUser.FullName = user.FullName;
            dbUser.IsEncrypted = user.IsEncrypted;
            dbUser.JiraPassword = user.JiraPassword;
            dbUser.JiraUserName = user.JiraUserName;

            dbUser.EncryptData(userSecret);

            await _userRepository.Update(dbUser);

            dbUser.DecryptData(userSecret);
            dbUser.ClearSensitiveData();

            return Json(dbUser);
        }

        //[HttpPost("{userName}")]
        //public async Task<IActionResult> SaveUser(SaveUserData data)
        //{
        //    throw new NotImplementedException("I'm not sure this method can even be done!");

            //if (string.IsNullOrEmpty(data?.UserName))
            //{
            //    _logger.LogWarning("Tried to save user with UserName = 'null'.");
            //    return BadRequest("UserName cannot be null.");
            //}

            //if (data?.User == null)
            //{
            //    _logger.LogWarning("Tried to save empty user.");
            //    return BadRequest("User cannot be null. Use DELETE to remove the user if that's what you wanted.");
            //}

            //if (data.UserName != data.User.UserName)
            //{
            //    _logger.LogWarning("Cannot change UserName of the user!");
            //    return BadRequest("UserName must not be different from 'User.UserName'!");
            //}

            //var existingUser = await _userRepository.FindByUserName(data.UserName);
            //if (existingUser == null)
            //{
            //    _logger.LogTrace($"Cannot find user with UserName = '{data.UserName}'.");
            //    return NotFound("Cannot find user with this UserName.");
            //}

            //await _userRepository.Update(data.User);
            //return NoContent();

        //}
    }
}