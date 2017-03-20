using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ReleaseControlPanel.API.Models;

namespace ReleaseControlPanel.API.Services
{
    public interface IJiraService
    {
        bool CheckConfiguration(User user, out object errorDetails);
        Task<Uri> CreateReleaseFilter(User user, string name, ProjectTags[] projectsTags, string[] uniqueTickets);
        Task<string[]> FilterNonExistingTickets(User user, string[] uniqueTickets);
        Task<JiraTicket[]> GetStories(User user, ProjectTags[] projectsTags, string[] uniqueTickets);
        Task<JiraTicket[]> GetStoriesForEpic(User user, string epicKey);
    }
}
