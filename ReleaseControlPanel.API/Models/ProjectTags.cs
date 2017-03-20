using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ReleaseControlPanel.API.Models
{
    public class ProjectTags
    {
        public string ProjectName { get; set; }
        public string[] Tags { get; set; }
    }
}
