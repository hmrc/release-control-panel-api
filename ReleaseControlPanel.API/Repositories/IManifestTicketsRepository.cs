using System.Threading.Tasks;
using MongoDB.Driver;
using ReleaseControlPanel.API.Models;

namespace ReleaseControlPanel.API.Repositories
{
    public interface IManifestTicketsRepository : IRepository
    {
        Task<DeleteResult> Delete(string id);
        Task<ManifestTickets[]> GetAll();
        Task Insert(ManifestTickets manifestTickets);
    }
}