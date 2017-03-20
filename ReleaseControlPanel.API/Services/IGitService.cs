using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using ReleaseControlPanel.API.Models;

namespace ReleaseControlPanel.API.Services
{
    public interface IGitService
    {
        bool CheckConfiguration(out object errorDetails);
        Task<ManifestTags[]> GetTagsForManifests(Manifest[] manifests);
        Task<ManifestTickets[]> GetTicketsForManifests(Manifest[] manifests);
        Task UpdateProjects();
    }
}
