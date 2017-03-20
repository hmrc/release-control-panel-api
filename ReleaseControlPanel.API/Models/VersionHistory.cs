using System;
namespace ReleaseControlPanel.API
{
	public class VersionHistory
	{
		public int Environment { get; set; }
		public string ProjectName { get; set; }
		public long LifeTime { get; set; }
		public string Version { get; set; }
	}
}
