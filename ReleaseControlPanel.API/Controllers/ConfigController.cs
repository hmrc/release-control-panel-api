using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ReleaseControlPanel.API.Models;

namespace ReleaseControlPanel.API.Controllers
{
    [Route("[controller]")]
    public class ConfigController : Controller
    {
        private readonly AppSettings _settings;

        public ConfigController(IOptions<AppSettings> settings)
        {
            _settings = settings.Value;
        }

        [HttpGet]
        public ClientConfig GetConfig()
        {
            return new ClientConfig
            {
                CiBuildUrl = _settings.CiBuildUrl,
                ProjectNames = _settings.Projects.Select(p => p.Name).ToArray()
            };
        }
    }
}