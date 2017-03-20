using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseControlPanel.API.Models;

namespace ReleaseControlPanel.API.Services.Impl
{
    internal class CiQaService : JenkinsService, ICiQaService
    {
        public override JenkinsType Type => JenkinsType.Qa;

        public CiQaService(ILogger<CiQaService> logger, IOptions<AppSettings> appOptions) : base(logger, appOptions)
        {
        }
    }
}
