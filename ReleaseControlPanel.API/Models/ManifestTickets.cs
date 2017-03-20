using MongoDB.Bson;

namespace ReleaseControlPanel.API.Models
{
    public class ManifestTickets
    {
        public ObjectId Id { get; set; }

        public string ManifestName { get; set; }
        public string[] Tickets { get; set; }
    }
}
