using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseControlPanel.API.Models;

namespace ReleaseControlPanel.API.Services.Impl
{
    internal class CiStagingService : JenkinsService, ICiStagingService
    {
        public override JenkinsType Type => JenkinsType.Staging;

        public CiStagingService(ILogger<CiStagingService> logger, IOptions<AppSettings> appOptions) : base(logger, appOptions)
        {
        }
    }
}
