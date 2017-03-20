using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using ReleaseControlPanel.API.Models;
using ReleaseControlPanel.API.Repositories;

namespace ReleaseControlPanel.API.Services.Impl
{
    internal class ManifestService : IManifestService
    {
        private readonly AppSettings _appSettings;
        private readonly ILogger _logger;
        private readonly IManifestRepository _manifestRepository;
        private readonly Semaphore _manifestSemaphore;

        public ManifestService(IOptions<AppSettings> appOptions, ILogger<ManifestService> logger, IManifestRepository manifestRepository)
        {
            _appSettings = appOptions.Value;
            _logger = logger;
            _manifestRepository = manifestRepository;

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _manifestSemaphore = new Semaphore(10, 10);
            }
        }

        public bool CheckConfiguration(out object errorDetails)
        {
            if (string.IsNullOrEmpty(_appSettings.ManifestIndexUrl))
            {
                _logger.LogError("Configuration error: 'App.ManifestIndexUrl' cannot be null or empty.");
                errorDetails = "Invalid server configuration. Check server logs for more information.";
                return false;
            }

            if (string.IsNullOrEmpty(_appSettings.ManifestUrlFormat))
            {
                _logger.LogError("Configuration error: 'App.ManifestUrlFormat' cannot be null or empty.");
                errorDetails = "Invalid server configuration. Check server logs for more information.";
                return false;
            }

            errorDetails = null;
            return true;
        }

        private async Task<Manifest> DownloadManifest(string manifestName)
        {
            object errorDetails;
            if (!CheckConfiguration(out errorDetails))
                return null;

            _manifestSemaphore?.WaitOne();

            var manifest = new Manifest { Name = manifestName, IsValid = false };
            try
            {
                _logger.LogTrace($"Loading manifest '{manifestName}' from Nexus.");
                var url = _appSettings.ManifestUrlFormat.Replace("{manifestName}", manifestName);

                Program.Client.DefaultRequestHeaders.Clear();
                var response = await Program.Client.GetAsync(new Uri(url));

                _logger.LogTrace($"Reading manifest '{manifestName}' content.");
                var content = await response.Content.ReadAsStringAsync();

                _logger.LogTrace($"Parsing manifest '{manifestName}' as JSON.");
                var manifestJson = JObject.Parse(content);

                _logger.LogTrace($"Loading applications from manifest  '{manifestName}' JSON.");
                var applicationsArray = manifestJson["applications"] as JArray;
                if (applicationsArray != null)
                {
                    manifest.ProjectVersions = applicationsArray
                        .Select(application => new ProjectVersion
                        {
                            Name = application["application_name"].Value<string>(),
                            Version = application["version"].Value<string>()
                        }).ToArray();

                    manifest.IsValid = manifest.ProjectVersions.All(pv => Regex.IsMatch(pv.Version, "^\\d+\\.\\d+(?:\\.\\d+)?$"));
                }
                else
                {
                    _logger.LogError($"Manifest '{manifestName}' doesn't have any applications!");
                }
            }
            catch (Exception e)
            {
                _logger.LogCritical($"An exception was raised when loading manifest '{manifestName}' from server: {e}");
                throw;
            }

            _manifestSemaphore?.Release();

            _logger.LogTrace($"Saving manifest '{manifestName}' to the database.");
            await _manifestRepository.Insert(manifest);

            return manifest;
        }

        public async Task<Manifest> GetManifest(string manifestName)
        {
            _logger.LogTrace($"Trying to find manifest '{manifestName}' in the database.");
            var existingManifest = await _manifestRepository.FindByName(manifestName);
            if (existingManifest != null)
            {
                _logger.LogTrace($"Manifest '{manifestName}' found in the database.");
                return existingManifest;
            }

            return await DownloadManifest(manifestName);
        }

        public async Task<string[]> GetManifestNames()
        {
            _logger.LogTrace("Beginning to load manifest names.");

            object errorDetails;
            if (!CheckConfiguration(out errorDetails))
            {
                return new string[0];
            }

            try
            {
                Program.Client.DefaultRequestHeaders.Clear();
                var response = await Program.Client.GetAsync(new Uri(_appSettings.ManifestIndexUrl));

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    _logger.LogError($"A request to get manifest names has failed: Nexus returned status '{response.StatusCode}'.");
                }

                _logger.LogTrace("Reading content as string.");
                var manifestContent = await response.Content.ReadAsStringAsync();

                _logger.LogTrace("Parsing manifest content using regular expressions.");
                const string versionRegexString = "<version>(.*?)</version>";
                var matches = Regex.Matches(manifestContent, versionRegexString, RegexOptions.IgnoreCase);

                _logger.LogTrace("Iterating over matches to build the list of manifest names.");
                return (
                    from Match match in matches
                    select match.Groups[1].Value
                ).ToArray();
            }
            catch (Exception e)
            {
                _logger.LogCritical($"An exception was raised when loading manifest names: {e}");
                throw;
            }
        }

        public async Task<Manifest[]> GetManifests()
        {
            _logger.LogTrace("Beginning to load all manifests.");

            object errorDetails;
            if (!CheckConfiguration(out errorDetails))
            {
                return new Manifest[0];
            }

            var manifestNames = (await GetManifestNames()).ToArray();
            var existingManifests = await _manifestRepository.FindAllByName(manifestNames);
            var missingManifestNames = (
                from mn in manifestNames
                where existingManifests.All(m => m.Name != mn)
                select mn
            ).ToArray();

            _logger.LogTrace($"Found that {missingManifestNames.Length} manifests are new and need to be downloaded.");
            var missingManifestTasks = missingManifestNames.Select(DownloadManifest).ToArray();
            await Task.WhenAll(missingManifestTasks);

            _logger.LogTrace("Joining manifests lists.");
            var missingManifests = missingManifestTasks.Select(mt => mt.Result).ToArray();
            var allManifests = existingManifests.Concat(missingManifests).ToArray();

            _logger.LogTrace("Returning manifests in the correct order.");
            return manifestNames.Select(mn => allManifests.FirstOrDefault(m => m.Name == mn)).Where(m => m.IsValid).ToArray();
        }
    }
}
