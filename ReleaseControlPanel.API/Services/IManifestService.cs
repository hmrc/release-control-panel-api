using System.Collections.Generic;
using System.Threading.Tasks;
using ReleaseControlPanel.API.Models;

namespace ReleaseControlPanel.API.Services
{
    public interface IManifestService
    {
        bool CheckConfiguration(out object errorDetails);
        Task<Manifest> GetManifest(string manifestName);
        Task<string[]> GetManifestNames();
        Task<Manifest[]> GetManifests();
    }
}
