using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ReleaseControlPanel.API.Models
{
    public class ManifestTags
    {
        public string ManifestName { get; set; }
        public ProjectTags[] ProjectsTags { get; set; }
    }
}
