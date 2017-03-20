using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ReleaseControlPanel.API.Models;

namespace ReleaseControlPanel.API.Repositories.Impl
{
    internal class ManifestRepository : BaseRepository, IManifestRepository
    {
        private IMongoCollection<Manifest> Manifests => Database.GetCollection<Manifest>("manifests");

        public ManifestRepository(ILogger<ManifestRepository> logger, IOptions<MongoDbSettings> settings)
            : base(logger, settings)
        {
        }

        public async Task<DeleteResult> Delete(string id)
        {
            return await Manifests.DeleteOneAsync(Builders<Manifest>.Filter.Eq("Id", id));
        }

        public async Task<Manifest> Get(string id)
        {
            return await Manifests
                .Find(Builders<Manifest>.Filter.Eq("Id", id))
                .FirstOrDefaultAsync();
        }

        public async Task<Manifest[]> FindAllByName(string[] names)
        {
            var manifestsList = await Manifests.Find(Builders<Manifest>.Filter.In("Name", names)).ToListAsync();
            return manifestsList.ToArray();
        }

        public async Task<Manifest> FindByName(string name)
        {
            return await Manifests
                .Find(Builders<Manifest>.Filter.Eq("Name", name))
                .FirstOrDefaultAsync();
        }

        public async Task Insert(Manifest manifest)
        {
            await Manifests.InsertOneAsync(manifest);
        }
    }
}
