using System.Threading.Tasks;

namespace ReleaseControlPanel.API.Repositories
{
    public interface IRepository
    {
        Task<bool> TestConnection();
    }
}