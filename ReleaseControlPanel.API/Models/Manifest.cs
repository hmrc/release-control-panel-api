using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ReleaseControlPanel.API.Models
{
    public class Manifest
    {
        public ObjectId Id { get; set; }
        public bool IsValid { get; set; }
        public string Name { get; set; }
        public ProjectVersion[] ProjectVersions { get; set; }
    }
}
