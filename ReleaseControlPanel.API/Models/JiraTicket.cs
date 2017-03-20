using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ReleaseControlPanel.API.Models
{
    public class JiraTicket
    {
        public string Author { get; set; }
        public string DateTime { get; set; }
        public string EpicKey { get; set; }
        //public JiraTicket Epic { get; set; }
        public string[] GitTags { get; set; }
        public string Message { get; set; }
        public string Status { get; set; }
        public string TicketNumber { get; set; }
        public string Url { get; set; }
    }
}
