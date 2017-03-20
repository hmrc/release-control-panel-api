using System.Threading.Tasks;
using MongoDB.Driver;
using ReleaseControlPanel.API.Models;

namespace ReleaseControlPanel.API.Repositories
{
    public interface IUserRepository : IRepository
    {
        Task<DeleteResult> Delete(string id);
        Task EnsureAdminExists();
        Task<User> FindByUserName(string userName);
        Task<User> Get(string id);
        Task Insert(User user);
        Task<bool> Update(User user);
    }
}