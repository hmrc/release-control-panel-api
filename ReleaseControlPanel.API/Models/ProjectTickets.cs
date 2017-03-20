using System.Collections.Generic;
using MongoDB.Bson;

namespace ReleaseControlPanel.API.Models
{
    public class ProjectTickets
    {
        public ObjectId Id { get; set; }

        public string ProjectName { get; set; }
        public string StartTag { get; set; }
        public string EndTag { get; set; }

        public string[] Tickets { get; set; }
    }
}
