using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using ReleaseControlPanel.API.Models;

namespace ReleaseControlPanel.API.Repositories.Impl
{
    internal abstract class BaseRepository : IRepository
    {
        protected readonly IMongoDatabase Database;
        protected readonly ILogger Logger;

        protected BaseRepository(ILogger logger, IOptions<MongoDbSettings> settings)
        {
            var client = new MongoClient(settings.Value.ConnectionString);
            Database = client.GetDatabase(settings.Value.Database);
            Logger = logger;
        }

        public async Task<bool> TestConnection()
        {
            Logger.LogDebug("Testing connection with MongoDB.");

            try
            {
                await Database.RunCommandAsync((Command<BsonDocument>) "{ping:1}");
                Logger.LogInformation("MongoDB connection works fine.");

                return true;
            }
            catch (TimeoutException)
            {
                Logger.LogCritical("A connection with MongoDB has timed out. Check if your server is working.");
            }
            catch (Exception ex)
            {
                Logger.LogCritical($"An exception was raised when connecting with mongodb: {ex}");
                throw;
            }

            return false;
        }
    }
}