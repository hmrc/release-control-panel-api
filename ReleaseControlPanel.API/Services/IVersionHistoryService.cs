using System;
using System.Threading.Tasks;

namespace ReleaseControlPanel.API
{
	public interface IVersionHistoryService
	{
		Task<VersionHistory[]> GetVersionsHistory();
	}
}
