using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ReleaseControlPanel.API.Models;

namespace ReleaseControlPanel.API.Repositories.Impl
{
    internal class ManifestTicketsRepository : BaseRepository, IManifestTicketsRepository
    {
        private IMongoCollection<ManifestTickets> ManifestsTickets => Database.GetCollection<ManifestTickets>("manifestsTickets");

        public ManifestTicketsRepository(ILogger<ManifestTicketsRepository> logger, IOptions<MongoDbSettings> settings)
            : base(logger, settings)
        {
        }

        public async Task<DeleteResult> Delete(string id)
        {
            return await ManifestsTickets.DeleteOneAsync(Builders<ManifestTickets>.Filter.Eq("Id", id));
        }

        public async Task<ManifestTickets[]> GetAll()
        {
            var tickets = await ManifestsTickets.FindAsync(_ => true);
            return tickets.ToList().ToArray();
        }

        public async Task Insert(ManifestTickets manifestTickets)
        {
            await ManifestsTickets.InsertOneAsync(manifestTickets);
        }
    }
}
