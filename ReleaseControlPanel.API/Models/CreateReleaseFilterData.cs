using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ReleaseControlPanel.API.Models
{
    public class CreateReleaseFilterData
    {
        public string EndReleaseName { get; set; }
        public string StartReleaseName { get; set; }
    }
}
