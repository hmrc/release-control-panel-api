using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseControlPanel.API.Models;

namespace ReleaseControlPanel.API.Services.Impl
{
    internal class CiBuildService : JenkinsService, ICiBuildService
    {
        public override JenkinsType Type => JenkinsType.Build;

        public CiBuildService(ILogger<CiBuildService> logger, IOptions<AppSettings> appOptions) : base(logger, appOptions)
        {
        }
    }
}
