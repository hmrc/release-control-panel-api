namespace ReleaseControlPanel.API.Models
{
    public class Release
    {
        public string Name { get; set; }
        public JiraTicket[] Tickets { get; set; }
    }
}