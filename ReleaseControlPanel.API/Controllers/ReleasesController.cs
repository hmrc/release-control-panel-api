using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ReleaseControlPanel.API.Models;
using ReleaseControlPanel.API.Repositories;
using ReleaseControlPanel.API.Services;

namespace ReleaseControlPanel.API.Controllers
{
    [Authorize]
    [Route("[controller]")]
    public class ReleasesController : Controller
    {
        private readonly IGitService _gitService;
        private readonly IJiraService _jiraService;
        private readonly ILogger _logger;
        private readonly IManifestService _manifestService;
        private readonly IProductionMonitorService _productionMonitorService;
        private readonly IUserRepository _userRepository;

        public ReleasesController(IGitService gitService,
            IJiraService jiraService,
            ILogger<ReleasesController> logger,
            IManifestService manifestService,
            IProductionMonitorService productionMonitorService,
            IUserRepository userRepository)
        {
            _gitService = gitService;
            _jiraService = jiraService;
            _logger = logger;
            _manifestService = manifestService;
            _productionMonitorService = productionMonitorService;
            _userRepository = userRepository;
        }

        [HttpPost("create-release-filter")]
        public async Task<IActionResult> CreateReleaseFilter(CreateReleaseFilterData data)
        {
            _logger.LogTrace("Starting to create a jira release filter");

            if (data == null)
            {
                _logger.LogError("Tried to create a release filter without giving start and end release.");
                return BadRequest("StartReleaseName and EndReleaseName must not be null.");
            }

            if (string.IsNullOrEmpty(data.StartReleaseName))
            {
                _logger.LogError("Tried to create a release filter with 'StartReleaseName' being null.");
                return BadRequest("StartReleaseName must not be null.");
            }

            if (string.IsNullOrEmpty(data.EndReleaseName))
            {
                _logger.LogError("Tried to create a release filter with 'EndReleaseName' being null.");
                return BadRequest("EndReleaseName must not be null.");
            }

            var currentUser = await _userRepository.FindByUserName(User.Identity.Name);
            if (currentUser == null)
            {
                _logger.LogError($"Could not find currently logged in user in the database. UserName: {User.Identity.Name}");
                return StatusCode(500, "Could not find currently logged in user in the dabase. Check server logs for more information.");
            }

            currentUser.DecryptData(User.FindFirst("UserSecret").Value);

            object errorDetails;
            if (!_jiraService.CheckConfiguration(currentUser, out errorDetails)
                || !_manifestService.CheckConfiguration(out errorDetails)
                || !_gitService.CheckConfiguration(out errorDetails))
            {
                return BadRequest(errorDetails);
            }

             _logger.LogTrace("Loading all available manifests.");
            var allManifests = await _manifestService.GetManifests();

            _logger.LogTrace("Finding which manifests fall within the start and end releases.");
            var shouldInsertManifests = false;
            var manifestsInRelease = new List<Manifest>();

            foreach (var manifest in allManifests)
            {
                if (!shouldInsertManifests && manifest.Name == data.StartReleaseName)
                    shouldInsertManifests = true;

                if (!shouldInsertManifests)
                    continue;

                manifestsInRelease.Add(manifest);

                if (manifest.Name == data.EndReleaseName)
                {
                    shouldInsertManifests = false;
                    break;
                }
            }

            if (shouldInsertManifests)
            {
                _logger.LogError($"An error has occurred when finding manifests for jira filter: Could not find end manifest for range from '{data.StartReleaseName}' to '{data.EndReleaseName}'.");
                return BadRequest("Could not create release filter. End release could not be found.");
            }

            if (manifestsInRelease.Count == 0)
            {
                _logger.LogError($"An error has occurred when finding manifests for jira filter: Could not find start manifest for name '{data.StartReleaseName}'.");
                return BadRequest("Could not create release filter. Start release could not be found.");
            }

            var manifestsInReleaseArray = manifestsInRelease.ToArray();
            _logger.LogTrace($"Found '{manifestsInReleaseArray.Length}' manifests falling within a given releases span.");

            _logger.LogTrace("Getting tags for all manifests.");
            var tagsForManifests = await _gitService.GetTagsForManifests(allManifests);

            _logger.LogTrace("Getting tickets for manifests falling within given manifest names.");
            var ticketsForManifests = await _gitService.GetTicketsForManifests(manifestsInReleaseArray);

            _logger.LogTrace("Making a combined list of tags for a given releases span.");
            var tagsForManifestsInRelease = tagsForManifests.Where(mt => manifestsInReleaseArray.Any(m => m.Name == mt.ManifestName)).ToArray();
            var uniqueProjectsNames = tagsForManifestsInRelease.SelectMany(mt => mt.ProjectsTags.Select(pt => pt.ProjectName)).Distinct().ToArray();
            var uniqueProjectsTags = uniqueProjectsNames.Select(pn => new ProjectTags
            {
                ProjectName = pn,
                Tags = tagsForManifestsInRelease.SelectMany(mt => mt.ProjectsTags.Where(pt => pt.ProjectName == pn).SelectMany(pt => pt.Tags)).Distinct().ToArray()
            }).ToArray();

            _logger.LogTrace("Making a combined list of tickets for a given releases span.");
            var uniqueTickets = ticketsForManifests.Where(mt => manifestsInReleaseArray.Any(m => m.Name == mt.ManifestName)).SelectMany(mt => mt.Tickets).Distinct().ToArray();

            _logger.LogTrace("Creating a jira filter for a given releases span.");
            var filterUri = await _jiraService.CreateReleaseFilter(currentUser, data.EndReleaseName, uniqueProjectsTags,
                uniqueTickets);

            if (filterUri == null)
            {
                _logger.LogError("Could not create a release filter. Check other logs to find out why.");
                return StatusCode(500, "An error has occurred while creating a release filter. Check server logs to find out why.");
            }

            return Json(new
            {
                name = data.EndReleaseName,
                url = filterUri.ToString()
            });
        }

        [HttpGet("production")]
        public async Task<IActionResult> GetCurrentProductionVersions()
        {
            object errorDetails;
            if (!_productionMonitorService.CheckConfiguration(out errorDetails))
            {
                return BadRequest(errorDetails);
            }

            return Json(await _productionMonitorService.GetProductionVersions());
        }

        [HttpGet("manifests")]
        public async Task<IActionResult> GetManifests()
        {
            object errorDetails;
            if (!_manifestService.CheckConfiguration(out errorDetails))
            {
                return BadRequest(errorDetails);
            }

            return Json(await _manifestService.GetManifests());
        }

        [HttpGet]
        //[ResponseCache(Duration = 60)] // TODO: Improve this caching. Sometimes it throws an exception when cache expires
        // System.ArgumentException: An item with the same key has already been added.
        public async Task<IActionResult> GetUpcomingReleases()
        {
            _logger.LogTrace("Getting upcoming releases.");
            var currentUser = await _userRepository.FindByUserName(User.Identity.Name);
            if (currentUser == null)
            {
                _logger.LogError($"Could not find currently logged in user in the database. UserName: {User.Identity.Name}");
                return StatusCode(500, "Could not find currently logged in user in the dabase. Check server logs for more information.");
            }

            currentUser.DecryptData(User.FindFirst("UserSecret").Value);

            object errorDetails;
            if (!_gitService.CheckConfiguration(out errorDetails) ||
                !_productionMonitorService.CheckConfiguration(out errorDetails) ||
                !_manifestService.CheckConfiguration(out errorDetails))
            {
                return BadRequest(errorDetails);
            }

            _logger.LogTrace("Starting the task of updating Git projects.");
            var updateProjectsTask = _gitService.UpdateProjects();

            _logger.LogTrace("Starting the task of loading production versions.");
            var getProductionVersionsTask = _productionMonitorService.GetProductionVersions();

            _logger.LogTrace("Starting the task of loading available manifests.");
            var getManifestsTask = _manifestService.GetManifests();

            _logger.LogTrace("Waiting for load tasks to complete.");
            await Task.WhenAll(updateProjectsTask, getProductionVersionsTask, getManifestsTask); 

            _logger.LogTrace("Starting the task of getting tickets for manifests.");
            var getTicketsForManifestsTask = _gitService.GetTicketsForManifests(getManifestsTask.Result);

            _logger.LogTrace("Starting the task of getting tags for manifests.");
            var getTagsForManifestsTask = _gitService.GetTagsForManifests(getManifestsTask.Result);

            _logger.LogTrace("Waiting for task to complete.");
            await Task.WhenAll(getTicketsForManifestsTask, getTagsForManifestsTask);
				
            _logger.LogTrace("Finding JIRA tickets which are incorrect.");
            var tickets = getTicketsForManifestsTask.Result.SelectMany(tfm => tfm.Tickets).Distinct().ToArray();
            var invalidJiraTickets = await _jiraService.FilterNonExistingTickets(currentUser, tickets);

            _logger.LogTrace("Searching for JIRA tickets.");
			var projectsTags = getTagsForManifestsTask.Result.SelectMany(tfm => tfm.ProjectsTags)
													  .GroupBy(pt => pt.ProjectName)
													  .Select(g => new ProjectTags
													  {
															ProjectName = g.Key,
															Tags = g.SelectMany(pt => pt.Tags).ToArray()
													  })
			                                          .ToArray();
            var validTickets = tickets.Except(invalidJiraTickets).ToArray();
            var jiraTickets = await _jiraService.GetStories(currentUser, projectsTags, validTickets);

            _logger.LogTrace("Searching for JIRA epics");
            var jiraTicketsWithEpics = jiraTickets.Where(t => !string.IsNullOrEmpty(t.EpicKey)).ToArray();
            var epicsKeys = jiraTicketsWithEpics.Select(t => t.EpicKey).Distinct().ToArray();
            var jiraEpics = await _jiraService.GetStories(currentUser, null, epicsKeys);

            _logger.LogTrace("Adding building list of releases and their tickets");
            const int suspiciousNumberOfTickets = 23;
            var releases = new List<Release>();
            foreach (var manifest in getManifestsTask.Result)
            {
                var releaseTicketsKeys = new List<string>(); // This is just for keeping track of used tickets so the code won't be repeating them for tags
                var releaseTickets = new List<JiraTicket>();

                var manifestTickets = getTicketsForManifestsTask.Result.FirstOrDefault(mt => mt.ManifestName == manifest.Name);
                var manifestTags = getTagsForManifestsTask.Result.FirstOrDefault(mt => mt.ManifestName == manifest.Name);

                if (manifestTickets?.Tickets != null)
                {
                    var foundTickets = jiraTickets.Where(jt => manifestTickets.Tickets.Contains(jt.TicketNumber)).ToArray();
                    if (foundTickets.Length >= suspiciousNumberOfTickets)
                    {
                        _logger.LogWarning($"More than {suspiciousNumberOfTickets} tickets were found for manifest '{manifest.Name}': {foundTickets.Length}. If you think that this is an incorrect behaviour please check with Tomasz (aka. The Dev).");
                    }
                    releaseTickets.AddRange(foundTickets);
                    releaseTicketsKeys.AddRange(foundTickets.Select(t => t.TicketNumber));
                }

                if (manifestTags?.ProjectsTags != null)
                {
                    foreach (var projectTags in manifestTags.ProjectsTags)
                    {
                        if (projectTags.Tags == null)
                            continue;

                        var expectedGitTagNames = projectTags.Tags.Select(t => $"{projectTags.ProjectName}-{t}").ToArray();
                        var projectTickets = (
                            from jt in jiraTickets
                            where !releaseTicketsKeys.Contains(jt.TicketNumber) && jt.GitTags.Any(gt => expectedGitTagNames.Contains(gt))
                            select jt
                        ).ToArray();
                        releaseTickets.AddRange(projectTickets);
                        releaseTicketsKeys.AddRange(projectTickets.Select(t => t.TicketNumber));
                    }
                }

                if (releaseTickets.Count >= suspiciousNumberOfTickets)
                {
                    _logger.LogWarning($"More than {suspiciousNumberOfTickets} tickets in total (after adding the ones by tags) were found for manifest '{manifest.Name}': {releaseTickets.Count}. If you think that this is an incorrect behaviour please check with Tomasz (aka. The Dev).");
                }
                
                releases.Add(new Release
                {
                    Name = manifest.Name,
                    Tickets = releaseTickets.ToArray()
                });
            }

            return Json(new
            {
                epics = jiraEpics,
                productionVersions = getProductionVersionsTask.Result,
                releasesTickets = releases,
                upcomingReleases = getManifestsTask.Result
            });
        }
    }
}