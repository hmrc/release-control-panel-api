using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReleaseControlPanel.API.Models;
using ReleaseControlPanel.API.Repositories;

namespace ReleaseControlPanel.API.Services.Impl
{
    internal class GitService : IGitService
    {
        private string GitRoot => Path.Combine(Directory.GetCurrentDirectory(), _settings.GitRepositoriesPath);
        private string ScriptsPath => Path.Combine(Directory.GetCurrentDirectory(), "InternalScripts");

        private readonly ILogger _logger;
        private readonly IManifestTicketsRepository _manifestTicketsRepository;
        private readonly AppSettings _settings;

        public GitService(ILogger<GitService> logger, IManifestTicketsRepository manifestTicketsRepository, IOptions<AppSettings> settings)
        {
            _logger = logger;
            _manifestTicketsRepository = manifestTicketsRepository;
            _settings = settings.Value;
        }

        public bool CheckConfiguration(out object errorDetails)
        {
            if (string.IsNullOrEmpty(_settings.GitRepositoriesPath))
            {
                _logger.LogCritical("Config error: 'App.GitRepositoriesPath' must be set to correct name of directory!");
                errorDetails = "Invalid server configuration. Check server logs for more information.";
                return false;
            }

            if (_settings.Projects == null || _settings.Projects.Length == 0)
            {
                _logger.LogError("Config: 'App.Projects' is empty. The tool requies some projects to be defined!");
                errorDetails = "Invalid server configuration. Check server logs for more information.";
                return false;
            }

            errorDetails = null;
            return true;
        }

        private void CloneProject(ProjectSettings project)
        {
            var projectPath = GetProjectPath(project);

            _logger.LogTrace($"Cloning project '{project.Name}' into '{projectPath}'.");

            var gitCloneStartInfo = new ProcessStartInfo("git", $"clone {project.GitUrl} {project.Name}")
            {
                WorkingDirectory = GitRoot
            };

            using (var gitCloneProcess = Process.Start(gitCloneStartInfo))
            {
                gitCloneProcess.WaitForExit();
            }
        }

        private void EnsureProjectsCloned()
        {
            if (!Directory.Exists(GitRoot))
            {
                _logger.LogTrace($"'GitRoot' directory doesn't exist. Creating it: {GitRoot}");
                Directory.CreateDirectory(GitRoot);
            }

            _logger.LogTrace("Checking if projects are pulled from git");

            foreach (var project in _settings.Projects)
            {
                if (Directory.Exists(GetProjectPath(project)))
                {
                    _logger.LogTrace($"Project '{project.Name}' is already cloned.");
                    continue;
                }

                CloneProject(project);
            }
        }

        private Task<ProjectTags[]> FindTagsForProjects(ProjectSettings[] projects)
        {
            return Task.Run(() =>
            {
                var gitRepositoriesPath = Path.GetFullPath(_settings.GitRepositoriesPath);

                var projectsNames = projects.Select(p => p.Name).ToArray();
                var projectsJson = JsonConvert.SerializeObject(projectsNames);

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    projectsJson = projectsJson.Replace("\"", "\"\"");
                }
                else
                {
                    projectsJson = projectsJson.Replace("\"", "\\\"");
                }

                const string command = "python";
                var commandArgs = $"find-tags-for-projects.py -d \"{gitRepositoriesPath}\" -p \"{projectsJson}\"";

                var output = SafeExecuteScript(command, commandArgs);

                return JsonConvert.DeserializeObject<ProjectTags[]>(output);
            });
        }

        private string GetProjectPath(ProjectSettings project)
        {
            return Path.Combine(GitRoot, project.Name);
        }

        public async Task<ManifestTags[]> GetTagsForManifests(Manifest[] manifests)
        {
            if (manifests.Length == 0)
                return new ManifestTags[0];

            var projectsTagsResult = await FindTagsForProjects(_settings.Projects);

            var projectsTags = projectsTagsResult.ToDictionary(pt => pt.ProjectName, pt => pt.Tags);

            var firstManifest = manifests.First();
            var projectsLastUsedTagIndex = _settings.Projects.ToDictionary(p => p.Name, p =>
            {
                var manifestProjectVersion = firstManifest.ProjectVersions.FirstOrDefault(pv => pv.Name == p.Name);
                return manifestProjectVersion == null
                    ? 0
                    : Array.IndexOf(projectsTags[p.Name], manifestProjectVersion.Version);
            });

            var manifestsTags = new List<ManifestTags>();

            foreach (var manifest in manifests)
            {
                var manifestTags = new ManifestTags { ManifestName = manifest.Name };
                var manifestProjectsTags = new List<ProjectTags>();
                foreach (var project in manifest.ProjectVersions)
                {
                    // Manifests contains projects which aren't supported by this project's git integration.
                    // Skip them.
                    if (!projectsTags.ContainsKey(project.Name))
                        continue;

                    var currentProjectTags = projectsTags[project.Name];
                    var projectStartTagIndex = projectsLastUsedTagIndex[project.Name];
                    var projectEndTagIndex = Array.IndexOf(currentProjectTags, project.Version) + 1;

                    var projectTags = new ProjectTags { ProjectName = project.Name };
                    if (projectEndTagIndex != -1 && projectEndTagIndex >= projectStartTagIndex)
                    {
                        projectsLastUsedTagIndex[project.Name] = projectEndTagIndex;

                        projectTags.Tags =
                            currentProjectTags.Skip(projectStartTagIndex)
                                .Take(projectEndTagIndex - projectStartTagIndex)
                                .ToArray();
                    }
                    else
                    {
                        projectTags.Tags = new[] { project.Version };
                    }

                    manifestProjectsTags.Add(projectTags);
                }

                manifestTags.ProjectsTags = manifestProjectsTags.ToArray();
                manifestsTags.Add(manifestTags);
            }

            return manifestsTags.ToArray();
        }

        public async Task<ManifestTickets[]> GetTicketsForManifests(Manifest[] manifests)
        {
            _logger.LogTrace("Getting tickets for a list of given manifests.");

            if (manifests.Length == 0)
                return new ManifestTickets[0];

            var cachedManifestsTickets = await _manifestTicketsRepository.GetAll();
            var newManifests = new List<dynamic>();
            for (var manifestIndex = 1; manifestIndex < manifests.Length; manifestIndex++)
            {
                var currentManifest = manifests[manifestIndex];
                if (cachedManifestsTickets.Any(cm => cm.ManifestName == currentManifest.Name))
                    continue;

                newManifests.Add(new
                {
                    CurrentManifest = currentManifest,
                    PreviousManifest = manifests[manifestIndex - 1]
                });
            }

            if (newManifests.Count == 0)
                return cachedManifestsTickets;

            var gitRepositoriesPath = Path.GetFullPath(_settings.GitRepositoriesPath);

            var projectsNames = _settings.Projects.Select(p => p.Name).ToArray();
            var projectsJson = JsonConvert.SerializeObject(projectsNames);
            var manifestsJson = JsonConvert.SerializeObject(newManifests);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                projectsJson = projectsJson.Replace("\"", "\"\"");
            }
            else
            {
                projectsJson = projectsJson.Replace("\"", "\\\"");
            }

            const string command = "python";
            var commandArgs = $"find-tickets-for-manifests.py -d \"{gitRepositoriesPath}\" -p \"{projectsJson}\"";
            var output = SafeExecuteScript(command, commandArgs, manifestsJson);

            var newManifestsTickets = JsonConvert.DeserializeObject<ManifestTickets[]>(output);
            foreach (var newManifestTickets in newManifestsTickets)
            {
                await _manifestTicketsRepository.Insert(newManifestTickets);
            }

            return cachedManifestsTickets.Concat(newManifestsTickets).ToArray();
        }

        private string SafeExecuteScript(string command, string commandArgs, string input = null)
        {
            var gitLogStartInfo = new ProcessStartInfo(command, commandArgs)
            {
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = ScriptsPath,
            };

            //const int gitLogProcessTimeout = 5000;
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();
            using (var process = new Process() { StartInfo = gitLogStartInfo })
            {
                process.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data == null)
                        return;

                    errorBuilder.AppendLine(args.Data);
                };
                process.OutputDataReceived += (sender, args) =>
                {
                    if (args.Data == null)
                        return;

                    outputBuilder.AppendLine(args.Data);
                };

                process.Start();

                if (input != null)
                {
                    process.StandardInput.WriteLine(input);
                }

                process.BeginErrorReadLine();
                process.BeginOutputReadLine();
                

                process.WaitForExit();
                process.CancelErrorRead();
                process.CancelOutputRead();

                if (!process.HasExited)
                {
                    _logger.LogError($"A script '{commandArgs}' has timed out.");
                    process.Kill();
                    process.WaitForExit(5000);
                }

            }

            var errorOutput = errorBuilder.ToString();
            if (!string.IsNullOrEmpty(errorOutput))
            {
                _logger.LogError($"An error was returned when executing script '{commandArgs}':\n{errorOutput}");
            }

            return outputBuilder.ToString();
        }

        private Task UpdateProject(ProjectSettings project)
        {
            return Task.Run(() =>
            {
                _logger.LogTrace($"Updating project '{project.Name}'.");

                var gitUpdateStartInfo = new ProcessStartInfo("git", "pull")
                {
                    WorkingDirectory = GetProjectPath(project)
                };

                using (var gitUpdateProcess = Process.Start(gitUpdateStartInfo))
                {
                    gitUpdateProcess.WaitForExit();
                }
            });
        }

        public async Task UpdateProjects()
        {
            object errorDetails;
            if (!CheckConfiguration(out errorDetails))
                return;

            EnsureProjectsCloned();

            var updateTasks = _settings.Projects.Select(UpdateProject);
            await Task.WhenAll(updateTasks);
        }
    }
}
