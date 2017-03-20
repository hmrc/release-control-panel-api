using System.Threading.Tasks;
using MongoDB.Driver;
using ReleaseControlPanel.API.Models;

namespace ReleaseControlPanel.API.Repositories
{
    public interface IManifestRepository : IRepository
    {
        Task<DeleteResult> Delete(string id);
        Task<Manifest[]> FindAllByName(string[] names);
        Task<Manifest> FindByName(string name);
        Task<Manifest> Get(string id);
        Task Insert(Manifest user);
    }
}