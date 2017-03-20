using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using ReleaseControlPanel.API.Models;

namespace ReleaseControlPanel.API.Services.Impl
{
    internal class ProductionMonitorService : IProductionMonitorService
    {
        private readonly AppSettings _appSettings;
        private readonly ILogger _logger;

        public ProductionMonitorService(IOptions<AppSettings> appOptions, ILogger<ProductionMonitorService> logger)
        {
            _appSettings = appOptions.Value;
            _logger = logger;
        }

        public bool CheckConfiguration(out object errorDetails)
        {
            if (_appSettings.Projects == null || _appSettings.Projects.Length == 0)
            {
                _logger.LogWarning("Configuration: 'App.Projects' is not set. Returning empty list of production versions as further code will make no difference.");
                errorDetails = "Invalid server configuration. Check server logs for more information.";
                return false;
            }

            errorDetails = null;
            return true;
        }

        public async Task<ProjectVersion[]> GetProductionVersions()
        {
            _logger.LogTrace($"Loading production versions from '{_appSettings.ProdUrl}'.");

            object errorDetails;
            if (!CheckConfiguration(out errorDetails))
            {
                return new ProjectVersion[0];
            }

            try
            {
                Program.Client.DefaultRequestHeaders.Clear();
                var response = await Program.Client.GetAsync(new Uri(_appSettings.ProdUrl));

                _logger.LogTrace("Reading content as string.");
                var jsonBody = await response.Content.ReadAsStringAsync();

                _logger.LogTrace("Parding production versions body as json object.");
                var json = JArray.Parse(jsonBody);

                _logger.LogTrace("Filtering projects and returning production versions.");

                return (
                    from s in json
                    where _appSettings.Projects.Any(p => p.Name == s["an"].Value<string>())
                    select new ProjectVersion
                    {
                        Name = s["an"].Value<string>(),
                        Version = s["ver"].Value<string>()
                    }
                ).ToArray();
            }
            catch (Exception e)
            {
                _logger.LogCritical($"An exception was raised when loading production versions: {e}");
                throw;
            }
        }
    }
}
