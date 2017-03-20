using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ReleaseControlPanel.API.Controllers
{
    [Route("[controller]")]
    public class StatisticsController : Controller
    {
		private readonly ILogger _logger;
		private readonly IVersionHistoryService _versionHistoryService;

		public StatisticsController(ILogger<StatisticsController> logger, IVersionHistoryService versionHistoryService)
		{
			_logger = logger;
			_versionHistoryService = versionHistoryService;
		}

		[HttpGet]
		public async Task<IActionResult> Get()
        {
			var versionsHistory = await _versionHistoryService.GetVersionsHistory();
			return Json(versionsHistory);
        }
    }
}