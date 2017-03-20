using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReleaseControlPanel.API.Models;

namespace ReleaseControlPanel.API.Services.Impl
{
    internal class JiraService : IJiraService
    {
		struct GetStoriesResult
		{
			public JiraTicket[] tickets;
			public int ticketsLeft;
			public int totalTickets;
		}

        private readonly AppSettings _appSettings;
        private readonly ILogger _logger;

        public JiraService(IOptions<AppSettings> appOptions, ILogger<JiraService> logger)
        {
            _appSettings = appOptions.Value;
            _logger = logger;
        }

        public bool CheckConfiguration(User user, out object errorDetails)
        {
            if (string.IsNullOrEmpty(_appSettings.JiraUrl))
            {
                _logger.LogError("Configuration error: 'App.JiraUrl' cannot be null or empty.");
                errorDetails = "Invalid server configuration. Check server logs for more information.";
                return false;
            }

            if (string.IsNullOrEmpty(user.JiraUserName))
            {
                _logger.LogError($"Invalid user configuration: JiraUserName is null or empty. UserName = '{user.UserName}'.");
                errorDetails = $"Invalid user configuration: JiraUserName is null or empty.";
                return false;
            }

            if (string.IsNullOrEmpty(user.JiraPassword))
            {
                _logger.LogError($"Invalid user configuration: JiraPassword is null or empty. UserName = '{user.UserName}'.");
                errorDetails = $"Invalid user configuration: JiraPassword is null or empty.";
                return false;
            }

            errorDetails = null;
            return true;
        }

        public async Task<Uri> CreateReleaseFilter(User user, string name, ProjectTags[] projectsTags, string[] uniqueTickets)
        {
            object errorDetails;
            if (!CheckConfiguration(user, out errorDetails))
            {
                _logger.LogError("Tried to find non existing jira tickets with invalid configuration.");
                return null;
            }

            try
            {
                Program.Client.DefaultRequestHeaders.Clear();
                Program.Client.SetBasicCredentials(user.JiraUserName, user.JiraPassword);

                var args = new
                {
                    description = $"List of tasks included in '{name}' release.",
                    favourite = true,
                    name = name,
                    jql = PrepareSearchJql(projectsTags, uniqueTickets)
                };
                var contentJson = JsonConvert.SerializeObject(args);
                var content = new StringContent(contentJson, Encoding.UTF8, "application/json");
                var uriString = $"{_appSettings.JiraUrl}rest/api/2/filter";

                _logger.LogTrace("Starting request to create jira filter.");
                var response = await Program.Client.PostAsync(new Uri(uriString), content);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    _logger.LogError($"A request to create jira filter has failed with status '{response.StatusCode}'.");
                    return null;
                }

                _logger.LogTrace("A request to create jira filter has finished. Reading response content.");
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogTrace("Parsing jira filter creation response content as JSON object.");
                var filterData = JObject.Parse(responseContent);

                return new Uri(filterData["viewUrl"].Value<string>());
            }
            catch (Exception ex)
            {
                var exceptionDetails = ex.ToString();
                if (ex.InnerException != null)
                {
                    exceptionDetails += "\nInner exception: {ex.InnerException}";
                }
                _logger.LogCritical($"An exception was raised when creating release filter: {exceptionDetails}");
                throw;
            }
        }

        public async Task<string[]> FilterNonExistingTickets(User user, string[] uniqueTickets)
        {
            object errorDetails;
            if (!CheckConfiguration(user, out errorDetails))
            {
                _logger.LogError("Tried to find non existing jira tickets with invalid configuration.");
                return new string[0];
            }

            try
            {
                Program.Client.DefaultRequestHeaders.Clear();
                Program.Client.SetBasicCredentials(user.JiraUserName, user.JiraPassword);

                var args = new
                {
                    jql = PrepareSearchJql(null, uniqueTickets),
                    maxResults = 1
                };
                var contentJson = JsonConvert.SerializeObject(args);
                var content = new StringContent(contentJson, Encoding.UTF8, "application/json");
                var uriString = $"{_appSettings.JiraUrl}rest/api/2/search";

                _logger.LogTrace($"Starting the request to find incorrect JIRA tickets. Tickets count: {uniqueTickets.Length}");
                var response = await Program.Client.PostAsync(new Uri(uriString), content);

                _logger.LogTrace("Request to find incorrect jira tickets has finished.");
                if (response.StatusCode != HttpStatusCode.BadRequest)
                    return new string[0];

                var responseBody = await response.Content.ReadAsStringAsync();
                const string jiraTicketRegex = "[A-Z]+[-_]\\d+";
                var matches = Regex.Matches(responseBody, jiraTicketRegex, RegexOptions.IgnoreCase);

                return (
                    from Match m in matches
                    select m.Value
                ).ToArray();
            }
            catch (Exception ex)
            {
                var exceptionDetails = ex.ToString();
                if (ex.InnerException != null)
                {
                    exceptionDetails += "\nInner exception: {ex.InnerException}"; 
                }
                _logger.LogCritical($"An exception was raised when finding incorrect jira tickets: {exceptionDetails}");
                throw;
            }
        }

        public async Task<JiraTicket[]> GetStories(User user, ProjectTags[] projectsTags, string[] uniqueTickets)
        {
            object errorDetails;
            if (!CheckConfiguration(user, out errorDetails))
            {
                _logger.LogError("Tried to get JIRA tickets with invalid configuration.");
                return new JiraTicket[0];
            }
			var ticketsList = new List<JiraTicket>();
			var ticketsLeft = 0;
			do
			{
				var result = await GetStoriesForJQL(user, PrepareSearchJql(projectsTags, uniqueTickets), ticketsList.Count);
				ticketsList.AddRange(result.tickets);

				ticketsLeft = result.ticketsLeft;
			}
			while (ticketsLeft > 0);

			return ticketsList.ToArray();
        }

        public async Task<JiraTicket[]> GetStoriesForEpic(User user, string epicKey)
        {
            object errorDetails;
            if (!CheckConfiguration(user, out errorDetails))
            {
                _logger.LogError("Tried to get JIRA tickets with invalid configuration.");
                return new JiraTicket[0];
            }

			var ticketsList = new List<JiraTicket>();
			var ticketsLeft = 0;
			do
			{
				var result = await GetStoriesForJQL(user, PrepareEpicStoriesSearchJql(epicKey), ticketsList.Count);
				ticketsList.AddRange(result.tickets);

				ticketsLeft = result.ticketsLeft;
			}
			while (ticketsLeft > 0);

			return ticketsList.ToArray();
        }

		private async Task<GetStoriesResult> GetStoriesForJQL(User user, string jql, int startIndex)
        {
            _logger.LogTrace("Starting to get JIRA tickets.");
            try
            {
                _logger.LogTrace("Setting user credentials.");
                Program.Client.DefaultRequestHeaders.Clear();
                Program.Client.SetBasicCredentials(user.JiraUserName, user.JiraPassword);

                var args = new
                {
                    fields = new[] { "creator", "updated", "customfield_10008", "customfield_10900", "summary", "status" },
                    jql = jql,
					startAt = startIndex,
					maxResults = 99999
                };
                var contentJson = JsonConvert.SerializeObject(args);
                var content = new StringContent(contentJson, Encoding.UTF8, "application/json");
                var uriString = $"{_appSettings.JiraUrl}rest/api/2/search";

                _logger.LogDebug("Starting the request to get JIRA tickets.");
                var response = await Program.Client.PostAsync(new Uri(uriString), content);

				if (response.StatusCode != HttpStatusCode.OK)
				{
					_logger.LogError($"A request to get JIRA tickets has failed with status code '{response.StatusCode}'.");

					return new GetStoriesResult
					{
						tickets = new JiraTicket[0],
						ticketsLeft = 0,
						totalTickets = 0
					};
				}

                _logger.LogDebug("Request to get jira tickets has finished.");
                var responseBody = await response.Content.ReadAsStringAsync();

                _logger.LogTrace("Parsing the response from JIRA.");
                var jiraTicketsObject = JObject.Parse(responseBody);

                var issuesArray = (JArray)jiraTicketsObject["issues"];
				var totalTickets = jiraTicketsObject["total"].Value<int>();
				var ticketsLeft = totalTickets - startIndex - issuesArray.Count;

				return new GetStoriesResult
				{
					tickets = issuesArray.Select(i =>
					{
						var tags = i["fields"]["customfield_10900"];
						var gitTags = tags.HasValues ? tags.Values<string>().ToArray() : new string[0];

						return new JiraTicket
						{
							Author = i["fields"]["creator"]["displayName"].Value<string>(),
							DateTime = i["fields"]["updated"].Value<string>(),
							EpicKey = i["fields"]["customfield_10008"].Value<string>(),
							GitTags = gitTags,
							Message = i["fields"]["summary"].Value<string>(),
							Status = i["fields"]["status"]["name"].Value<string>(),
							TicketNumber = i["key"].Value<string>(),
							Url = $"{_appSettings.JiraUrl}browse/{i["key"].Value<string>()}"
						};
					}).ToArray(),
					ticketsLeft = ticketsLeft,
					totalTickets = totalTickets
				};
            }
            catch (Exception ex)
            {
                var exceptionDetails = ex.ToString();
                if (ex.InnerException != null)
                {
                    exceptionDetails += "\nInner exception: {ex.InnerException}"; 
                }
                _logger.LogCritical($"An exception was raised when getting jira tickets: {exceptionDetails}");
                throw;
            }
        }

        private string PrepareEpicStoriesSearchJql(string epicKey)
        {
			return $"project = \"{_appSettings.TeamName}\" and \"Epic Link\" = \"{epicKey}\" ORDER BY status ASC, team ASC, key DESC";
        }

        private string PrepareSearchJql(ProjectTags[] projectsAndTags, string[] tickets)
        {
            var uniqueTickets = tickets.Distinct().ToArray();

            var queries = new List<string>();
            if (projectsAndTags != null && projectsAndTags.Length > 0)
            {
                var gitTags = string.Join(
                    ",",
                    from pt in projectsAndTags
                    where pt.Tags.Length > 0
                    select string.Join(",", pt.Tags.Select(ptt => $"{pt.ProjectName}-{ptt}"))
                );
                queries.Add($"\"Git Tag\" in ({gitTags})");
            }

            if (uniqueTickets.Length > 0)
            {
                queries.Add($"Key in ({string.Join(",", uniqueTickets)})");
            }

			return $"project = \"{_appSettings.TeamName}\" AND ({string.Join(" OR ", queries)}) ORDER BY status ASC, team ASC, key DESC";
        }
    }
}
