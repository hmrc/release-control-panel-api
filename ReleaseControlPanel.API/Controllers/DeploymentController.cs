using System.Collections.Generic;
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
    [Route("deploy")]
    public class DeploymentController : Controller
    {
        private readonly AppSettings _appSettings;
        private readonly ILogger _logger;
        private readonly ICiQaService _qaService;
        private readonly ICiStagingService _stagingService;
        private readonly IUserRepository _userRepository;

        public DeploymentController(IOptions<AppSettings> appOptions,
            ILogger<DeploymentController> logger,
            ICiQaService qaService,
            ICiStagingService stagingService,
            IUserRepository userRepository)
        {
            _appSettings = appOptions.Value;
            _logger = logger;
            _qaService = qaService;
            _stagingService = stagingService;
            _userRepository = userRepository;
        }

        [HttpPost("qa")]
        public async Task<IActionResult> DeployToQa([FromBody] StartDeploymentData startDeploymentData)
        {
            var currentUser = await _userRepository.FindByUserName(User.Identity.Name);
            if (currentUser == null)
            {
                _logger.LogError($"Could not find currently logged in user in the database. UserName: {User.Identity.Name}");
                return StatusCode(500, "Could not find currently logged in user in the dabase. Check server logs for more information.");
            }

            currentUser.DecryptData(User.FindFirst("UserSecret").Value);

            object errorDetails;
            if (!_qaService.CheckConfiguration(currentUser, out errorDetails))
            {
                return BadRequest(errorDetails);
            }

            if (string.IsNullOrEmpty(startDeploymentData?.ProjectName))
            {
                _logger.LogWarning("Name cannot be null or empty when starting deployment to QA.");
                return BadRequest("Name cannot be null or empty when starting deployment to QA.");
            }

            if (string.IsNullOrEmpty(startDeploymentData.Version))
            {
                _logger.LogWarning("Project version cannot be null or empty when starting deployment to QA.");
                return BadRequest("Project version cannot be null or empty when starting deployment to QA.");
            }

            if (_appSettings.Projects == null || _appSettings.Projects.Length == 0)
            {
                _logger.LogWarning("Config: 'App.Projects' is empty. The tool requies some projects to be defined!");
                return StatusCode(500, "Incorrect server configuration. Check logs for more information.");
            }

            if (_appSettings.Projects.All(p => p.Name != startDeploymentData.ProjectName))
            {
                _logger.LogWarning($"Cannot find a project to deploy with a name '{startDeploymentData.ProjectName}'.");
                return BadRequest($"Cannot find a project to deploy with a name '{startDeploymentData.ProjectName}'.");
            }

            if (string.IsNullOrEmpty(_appSettings.QaDeploymentJobName))
            {
                _logger.LogError("Config: 'App.QaDeploymentJobName' has not been set correctly.");
                return StatusCode(500, "Incorrect server configuration. Check logs for more information.");
            }

            var deployOptions = new []
            {
                new KeyValuePair<string, string>("APP", startDeploymentData.ProjectName),
                new KeyValuePair<string, string>("VERSION", startDeploymentData.Version),
                new KeyValuePair<string, string>("delay", "50sec"), 
            };

            await _qaService.StartBuild(currentUser, _appSettings.QaDeploymentJobName, deployOptions);

            return NoContent();
        }

        [HttpPost("staging")]
        public async Task<IActionResult> DeployToStaging([FromBody] StartDeploymentData startDeploymentData)
        {
            var currentUser = await _userRepository.FindByUserName(User.Identity.Name);
            if (currentUser == null)
            {
                _logger.LogError($"Could not find currently logged in user in the database. UserName: {User.Identity.Name}");
                return StatusCode(500, "Could not find currently logged in user in the dabase. Check server logs for more information.");
            }

            currentUser.DecryptData(User.FindFirst("UserSecret").Value);

            object errorDetails;
            if (!_stagingService.CheckConfiguration(currentUser, out errorDetails))
            {
                return BadRequest(errorDetails);
            }

            if (string.IsNullOrEmpty(startDeploymentData?.ProjectName))
            {
                _logger.LogWarning("Name cannot be null or empty when starting deployment to Staging.");
                return BadRequest("Name cannot be null or empty when starting deployment to Staging.");
            }

            if (string.IsNullOrEmpty(startDeploymentData.Version))
            {
                _logger.LogWarning("Project version cannot be null or empty when starting deployment to Staging.");
                return BadRequest("Project version cannot be null or empty when starting deployment to Staging.");
            }

            if (_appSettings.Projects == null || _appSettings.Projects.Length == 0)
            {
                _logger.LogWarning("Config: 'App.Projects' is empty. The tool requies some projects to be defined!");
                return StatusCode(500, "Incorrect server configuration. Check logs for more information.");
            }

            if (_appSettings.Projects.All(p => p.Name != startDeploymentData.ProjectName))
            {
                _logger.LogWarning($"Cannot find a project to deploy with a name '{startDeploymentData.ProjectName}'.");
                return BadRequest($"Cannot find a project to deploy with a name '{startDeploymentData.ProjectName}'.");
            }

            if (string.IsNullOrEmpty(_appSettings.StagingDeploymentJobName))
            {
                _logger.LogError("Config: 'App.StagingDeploymentJobName' has not been set correctly.");
                return StatusCode(500, "Incorrect server configuration. Check logs for more information.");
            }

            var deployOptions = new[]
            {
                new KeyValuePair<string, string>("APP", startDeploymentData.ProjectName),
                new KeyValuePair<string, string>("VERSION", startDeploymentData.Version),
                new KeyValuePair<string, string>("delay", "50sec"),
            };

            await _stagingService.StartBuild(currentUser, _appSettings.StagingDeploymentJobName, deployOptions);

            return NoContent();
        }
    }
}