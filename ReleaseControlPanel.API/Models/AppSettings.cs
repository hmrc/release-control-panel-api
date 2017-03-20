namespace ReleaseControlPanel.API.Models
{
    public class AppSettings
    {
        public string AdministratorFullName { get; set; }
        public string AdministratorPassword { get; set; }
        public string AdministratorUserName { get; set; }
        public string CiBuildUrl { get; set; }
        public string CiQaUrl { get; set; }
        public string CiStagingUrl { get; set; }
		public EnvironmentSettings[] Environments { get; set; }
        public string GitRepositoriesPath { get; set; }
        public string ManifestIndexUrl { get; set; }
        public string ManifestUrlFormat { get; set; }
        public string JiraUrl { get; set; }
        public string ProdUrl { get; set; }
        public ProjectSettings[] Projects { get; set; }
        public string QaDeploymentJobName { get; set; }
        public string ReleasesHistoryUrl { get; set; }
        public string StagingDeploymentJobName { get; set; }
		public string TeamName { get; set; }
    }
}
