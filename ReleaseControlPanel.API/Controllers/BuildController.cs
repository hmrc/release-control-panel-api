using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseControlPanel.API.Models;
using ReleaseControlPanel.API.Repositories;
using ReleaseControlPanel.API.Services;

namespace ReleaseControlPanel.API.Controllers
{
    [Authorize]
    [Route("[controller]")]
    public class BuildController : Controller
    {
        private readonly AppSettings _appSettings;
        private readonly ICiBuildService _ciBuildService;
        private readonly ILogger _logger;
        private readonly IUserRepository _userRepository;

        public BuildController(IOptions<AppSettings> appOptions,
            ICiBuildService ciBuildService,
            ILogger<BuildController> logger,
            IUserRepository userRepository)
        {
            _appSettings = appOptions.Value;
            _ciBuildService = ciBuildService;
            _logger = logger;
            _userRepository = userRepository;
        }

        [HttpGet("statuses")]
        public async Task<IActionResult> GetBuildStatuses()
        {
            var currentUser = await _userRepository.FindByUserName(User.Identity.Name);
            if (currentUser == null)
            {
                _logger.LogError($"Could not find currently logged in user in the database. UserName: {User.Identity.Name}");
                return StatusCode(500, "Could not find currently logged in user in the dabase. Check server logs for more information.");
            }

            currentUser.DecryptData(User.FindFirst("UserSecret").Value);

            object errorDetails;
            if (!_ciBuildService.CheckConfiguration(currentUser, out errorDetails))
            {
                return BadRequest(errorDetails);
            }

            var getBuildStatusesTasks = _appSettings.Projects.Select(p => _ciBuildService.GetBuildStatus(currentUser, p.Name)).ToArray();

            await Task.WhenAll(getBuildStatusesTasks);

            var buildStatuses = getBuildStatusesTasks.Select(gbs => gbs.Result).ToArray();
            return Json(buildStatuses);
        }
    }
}