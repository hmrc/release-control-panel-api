namespace ReleaseControlPanel.API.Models
{
    public class BuildStatus
    {
        public string BuildNumber { get; set; }
        public bool IsBuilding { get; set; }
        public string ProjectName { get; set; }
        public string Version { get; set; }
    }
}