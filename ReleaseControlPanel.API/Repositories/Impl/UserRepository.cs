using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ReleaseControlPanel.API.Models;

namespace ReleaseControlPanel.API.Repositories.Impl
{
    internal class UserRepository : BaseRepository, IUserRepository
    {
        private IMongoCollection<User> Users => Database.GetCollection<User>("users");

        private readonly AppSettings _appSettings;

        public UserRepository(IOptions<AppSettings> appOptions, ILogger<UserRepository> logger, IOptions<MongoDbSettings> settings)
            : base(logger, settings)
        {
            _appSettings = appOptions.Value;
        }

        public async Task<DeleteResult> Delete(string id)
        {
            return await Users.DeleteOneAsync(Builders<User>.Filter.Eq("Id", id));
        }

        public async Task EnsureAdminExists()
        {
            Logger.LogTrace("Checking if administrator account exists.");
            var administrator = await FindByUserName("administrator");
            if (administrator != null)
            {
                Logger.LogTrace("Administrator account exists.");
                return;
            }

            Logger.LogTrace("Creating administrator account.");

            var admin = new User
            {
                Admin = true,
                FullName = _appSettings.AdministratorFullName,
                UserName = _appSettings.AdministratorUserName
            };
            admin.GenerateUserSecret();
            admin.EncryptData(admin.UserSecret);
            admin.SetPassword(_appSettings.AdministratorPassword);

            await Insert(admin);

            Logger.LogTrace("Administrator account created.");
        }

        public async Task<User> FindByUserName(string userName)
        {
            return await Users
                .Find(Builders<User>.Filter.Eq("UserName", userName))
                .FirstOrDefaultAsync();
        }

        public async Task<User> Get(string id)
        {
            return await Users
                .Find(Builders<User>.Filter.Eq("Id", id))
                .FirstOrDefaultAsync();
        }

        public async Task Insert(User user)
        {
            await Users.InsertOneAsync(user);
        }

        public async Task<bool> Update(User user)
        {
            var filter = Builders<User>.Filter.Eq("Id", user.Id);
            user.UpdatedOn = DateTime.Now;
            var replaceResult = await Users.ReplaceOneAsync(filter, user);

            return replaceResult.IsAcknowledged && replaceResult.ModifiedCount == 1;
        }
    }
}
