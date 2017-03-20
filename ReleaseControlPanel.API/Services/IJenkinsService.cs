using System.Collections.Generic;
using System.Threading.Tasks;
using ReleaseControlPanel.API.Models;

namespace ReleaseControlPanel.API.Services
{
    public interface IJenkinsService
    {
        JenkinsType Type { get; }

        bool CheckConfiguration(User user, out object errorDetails);
        Task<BuildStatus> GetBuildStatus(User user, string projectName);
        JenkinsCredentials GetCredentials(User user);
        Task<bool> StartBuild(User user, string projectName, KeyValuePair<string, string>[] args);
        Task<bool> TestConnection();
    }
}
