using System.Collections.Generic;
using System.Threading.Tasks;
using ReleaseControlPanel.API.Models;

namespace ReleaseControlPanel.API.Services
{
    public interface IProductionMonitorService
    {
        bool CheckConfiguration(out object errorDetails);
        Task<ProjectVersion[]> GetProductionVersions();
    }
}
