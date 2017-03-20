using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using ReleaseControlPanel.API.Models;

namespace ReleaseControlPanel.API
{
	internal class VersionHistoryService : IVersionHistoryService
	{
		private readonly ILogger _logger;
		private readonly AppSettings _settings;

		public VersionHistoryService(IOptions<AppSettings> appOptions, ILogger<VersionHistoryService> logger)
		{
			_logger = logger;
			_settings = appOptions.Value;
		}

		private Environments GetEnvironmentForName(string name)
		{
			foreach (var env in _settings.Environments)
			{
				if (env.NameRegex.IsMatch(name))
				{
					return env.Type;
				}
			}

			return Environments.Invalid;
		}

		public async Task<VersionHistory[]> GetVersionsHistory()
		{
			_logger.LogTrace("Starting to get versions history.");

			try
			{
				Program.Client.DefaultRequestHeaders.Clear();

				var response = await Program.Client.GetAsync(new Uri(_settings.ReleasesHistoryUrl));

				if (response.StatusCode != HttpStatusCode.OK)
				{
					_logger.LogError($"A request to get versions history has failed with status code '{response.StatusCode}'.");

					return new VersionHistory[0];
				}

				_logger.LogTrace("Request to get versions history has finished.");
				var responseBody = await response.Content.ReadAsStringAsync();

				_logger.LogTrace("Parsing the response.");
				var versions = JArray.Parse(responseBody);

				var versionsHistory = new List<VersionHistory>();
				foreach (JObject version in versions)
				{
					var versionEnvironment = GetEnvironmentForName(version["env"].Value<string>());
					if (versionEnvironment == Environments.Invalid)
						continue;

					var projectName = version["an"].Value<string>();
					if (!ProjectExists(projectName))
						continue;

					var firstSeen = version["fs"].Value<int>();
					var lastSeen = version["ls"].Value<int>();
					var versionNumber = version["ver"].Value<string>();

					versionsHistory.Add(new VersionHistory
					{
						ProjectName = projectName,
						Environment = (int)versionEnvironment,
						LifeTime = (lastSeen - firstSeen) * 1000, // Time is in milliseconds
						Version = versionNumber
					});
				}

				return versionsHistory.ToArray();
			}
			catch (Exception ex)
			{
				var exceptionDetails = ex.ToString();
				if (ex.InnerException != null)
				{
					exceptionDetails += $"\nInner exception: {ex.InnerException}";
				}
				_logger.LogCritical($"An exception was raised when getting versions history: {exceptionDetails}");
				throw;
			}
		}

		private bool ProjectExists(string name)
		{
			return _settings.Projects.Any(p => p.Name == name);
		}
	}
}
