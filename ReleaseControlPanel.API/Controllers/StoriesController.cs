using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ReleaseControlPanel.API.Models;
using ReleaseControlPanel.API.Repositories;
using ReleaseControlPanel.API.Services;

namespace ReleaseControlPanel.API.Controllers
{
    [Authorize]
    [Route("[controller]")]
    public class StoriesController : Controller
    {
        private readonly IJiraService _jiraService;
        private readonly ILogger _logger;
        private readonly IUserRepository _userRepository;

        public StoriesController(IJiraService jiraService, ILogger<StoriesController> logger, IUserRepository userRepository)
        {
            _jiraService = jiraService;
            _logger = logger;
            _userRepository = userRepository;
        }

        [HttpGet("for-epic/{epicKey}")]
        public async Task<IActionResult> GetStoriesForEpic(string epicKey)
        {
            if (string.IsNullOrEmpty(epicKey))
            {
                _logger.LogError("Tried to get stories for epic with epic key set to null.");
                return BadRequest("EpicKey must not me empty.");
            }

            var currentUser = await _userRepository.FindByUserName(User.Identity.Name);
            if (currentUser == null)
            {
                _logger.LogError($"Could not find currently logged in user in the database. UserName: {User.Identity.Name}");
                return StatusCode(500, "Could not find currently logged in user in the dabase. Check server logs for more information.");
            }

            currentUser.DecryptData(User.FindFirst("UserSecret").Value);

            object errorDetails;
            if (!_jiraService.CheckConfiguration(currentUser, out errorDetails))
            {
                return BadRequest(errorDetails);
            }

            var storiesForEpic = await _jiraService.GetStoriesForEpic(currentUser, epicKey);
            return Json(storiesForEpic);
        }

        [HttpGet("refresh")]
        public IEnumerable<Story> RefreshStatuses()
        {
            throw new NotImplementedException();
        }
    }
}