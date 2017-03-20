using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using ReleaseControlPanel.API.Models;

namespace ReleaseControlPanel.API.Services.Impl
{
    internal abstract class JenkinsService : IJenkinsService
    {
        public abstract JenkinsType Type { get; }

        private string Url
        {
            get
            {
                switch (Type)
                {
                    case JenkinsType.Build:
                        return _appSettings.CiBuildUrl;

                    case JenkinsType.Qa:
                        return _appSettings.CiQaUrl;

                    case JenkinsType.Staging:
                        return _appSettings.CiStagingUrl;

                    default:
                        _logger.LogCritical($"Incorrect Jenkins type is in use: {Type}");
                        throw new InvalidOperationException();
                }
            }
        }

        private readonly AppSettings _appSettings;
        private readonly ILogger _logger;

        protected JenkinsService(ILogger<JenkinsService> logger, IOptions<AppSettings> appOptions)
        {
            _logger = logger;
            _appSettings = appOptions.Value;
        }

        public bool CheckConfiguration(User user, out object errorDetails)
        {
            if (Type != JenkinsType.Build && Type != JenkinsType.Qa && Type != JenkinsType.Staging)
            {
                _logger.LogCritical($"Incorrect Jenkins type is in use: {Type}");
                errorDetails = "Invalid server configuration. Check server logs for more information.";
                return false;
            }

            if (string.IsNullOrEmpty(Url))
            {
                _logger.LogError($"Configuration error: Jenkins URL cannot be null or empty for type '{Type}'.");
                errorDetails = "Invalid server configuration. Check server logs for more information.";
                return false;
            }

            if (user.IsEncrypted)
            {
                _logger.LogError("Did you forget to decrypt user data before calling this method?");
                errorDetails = "Invalid server configuration. Check server logs for more information.";
                return false;
            }

            var credentials = GetCredentials(user);

            if (string.IsNullOrEmpty(credentials.UserName))
            {
                _logger.LogError($"Invalid user configuration: UserName is null or empty. UserName = '{user.UserName}', Jenkins = '{Type}'.");
                errorDetails = $"Invalid user configuration: UserName is null or empty. Jenkins = '{Type}'.";
                return false;
            }

            if (string.IsNullOrEmpty(credentials.ApiToken))
            {
                _logger.LogError($"Invalid user configuration: ApiToken is null or empty. UserName = '{user.UserName}', Jenkins = '{Type}'.");
                errorDetails = $"Invalid user configuration: ApiToken is null or empty. Jenkins = '{Type}'.";
                return false;
            }

            errorDetails = null;
            return true;
        }

        public async Task<BuildStatus> GetBuildStatus(User user, string projectName)
        {
            var credentials = GetCredentials(user);
            var networkCredentials = new NetworkCredential(credentials.UserName, credentials.ApiToken);

            using (var handler = new HttpClientHandler { Credentials = networkCredentials })
            using (var client = new HttpClient(handler))
            {
                var uriString = $"{Url}job/{projectName}/lastBuild/api/json";
                var response = await client.GetAsync(new Uri(uriString));
                var jsonBody = await response.Content.ReadAsStringAsync();

                // TODO: Parsing... 

                return null;
            }
        }

        public JenkinsCredentials GetCredentials(User user)
        {
            switch (Type)
            {
                case JenkinsType.Build:
                    return new JenkinsCredentials
                    {
                        ApiToken = user.CiBuildApiToken,
                        UserName = user.CiBuildUserName
                    };

                case JenkinsType.Qa:
                    return new JenkinsCredentials
                    {
                        ApiToken = user.CiQaApiToken,
                        UserName = user.CiQaUserName
                    };

                case JenkinsType.Staging:
                    return new JenkinsCredentials
                    {
                        ApiToken = user.CiStagingApiToken,
                        UserName = user.CiStagingUserName
                    };

                default:
                    _logger.LogCritical($"Incorrect Jenkins type is in use: {Type}");
                    throw new InvalidOperationException();
            }
        }

        public async Task<bool> StartBuild(User user, string projectName, KeyValuePair<string, string>[] args)
        {
            var credentials = GetCredentials(user);

            try
            {
                Program.Client.DefaultRequestHeaders.Clear();
                Program.Client.SetBasicCredentials(credentials.UserName, credentials.ApiToken);

                var argsQueryString = string.Join("&", args.Select(arg => $"{arg.Key}={arg.Value}"));
                var uriString = $"{Url}job/{projectName}/buildWithParameters?{argsQueryString}";
                var response = await Program.Client.PostAsync(new Uri(uriString), new StringContent(""));

                return response.StatusCode == HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                _logger.LogCritical($"An exception was raised when starting a build in Jenkins '{Type}': {ex}");
                throw;
            }
        }

        public async Task<bool> TestConnection()
        {
            _logger.LogDebug($"Checking VPN connection by connecting with Jenkins '{Type}'.");

            using (var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                try
                {
                    Program.Client.DefaultRequestHeaders.Clear();

                    await Program.Client.GetAsync(new Uri($"{Url}api/json"), cancellationTokenSource.Token);

                    _logger.LogInformation($"Connection with Jenkins '{Type}' seems to be OK.");
                    return true;
                }
                catch (TaskCanceledException)
                {
                    _logger.LogCritical($"Could not connect with Jenkins '{Type}'. Check if you're connected to VPN first!");

                }
                catch (Exception ex)
                {
                    _logger.LogCritical($"An exception was raised when starting a build in Jenkins '{Type}': {ex}");
                    throw;
                }

                return false;
            }
        }
    }
}
