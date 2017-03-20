using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using ReleaseControlPanel.API.Models;
using ReleaseControlPanel.API.Repositories;
using ReleaseControlPanel.API.Repositories.Impl;
using ReleaseControlPanel.API.Services;
using ReleaseControlPanel.API.Services.Impl;

namespace ReleaseControlPanel.API
{
    public class Startup
    {
        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline
        public async void Configure(IApplicationBuilder app,
            IHostingEnvironment env,
            ICiBuildService ciBuildService,
            ICiQaService ciQaService,
            ICiStagingService ciStagingService,
            ILoggerFactory loggerFactory,
            IOptions<AuthSettings> settings,
            IUserRepository userRepository)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddFile("logs/{Date}.txt");
            loggerFactory.AddDebug();

            app.UseResponseCompression();

            app.UseCors("DevCorsPolicy");

            app.UseCookieAuthentication(new CookieAuthenticationOptions
            {
                AuthenticationScheme = settings.Value.AuthenticationScheme,
                LoginPath = PathString.Empty,
                AccessDeniedPath = PathString.Empty,
                AutomaticAuthenticate = true,
                AutomaticChallenge = true,
                Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin = context =>
                    {
                        context.Response.StatusCode = 401;
                        return Task.FromResult(0);
                    }
                }
            });

            app.UseResponseCaching();

            app.UseMvc();

            var dbWorks = await userRepository.TestConnection();
            var ciBuildWorks = await ciBuildService.TestConnection();
            var ciQaWorks = await ciQaService.TestConnection();
            var ciStagingWorks = await ciStagingService.TestConnection();
            if (!dbWorks || !ciBuildWorks || !ciQaWorks || !ciStagingWorks)
            {
                Environment.Exit(1);
                return;
            }

            await userRepository.EnsureAdminExists();
        }

        // This method gets called by the runtime. Use this method to add services to the container
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors(options =>
            {
                options.AddPolicy("DevCorsPolicy", builder =>
                {
                    builder.AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });

            services.AddResponseCaching();

            // Add framework services.
            services.AddApplicationInsightsTelemetry(Configuration);
            
            services.AddMvc();

            services.AddResponseCompression(options =>
            {
                options.Providers.Add<GzipCompressionProvider>();
            });

            services.Configure<AppSettings>(Configuration.GetSection("App"));
            services.Configure<AuthSettings>(Configuration.GetSection("Auth"));
            services.Configure<MongoDbSettings>(Configuration.GetSection("MongoDb"));

            services.AddSingleton<IUserRepository, UserRepository>();
            services.AddSingleton<IManifestRepository, ManifestRepository>();
            services.AddSingleton<IManifestTicketsRepository, ManifestTicketsRepository>();
            services.AddSingleton<IGitService, GitService>();
            services.AddSingleton<ICiBuildService, CiBuildService>();
            services.AddSingleton<ICiQaService, CiQaService>();
            services.AddSingleton<ICiStagingService, CiStagingService>();
            services.AddSingleton<IProductionMonitorService, ProductionMonitorService>();
            services.AddSingleton<IManifestService, ManifestService>();
            services.AddSingleton<IJiraService, JiraService>();
			services.AddSingleton<IVersionHistoryService, VersionHistoryService>();
        }

        public Startup(IHostingEnvironment env )
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

            if (env.IsEnvironment("Development"))
            {
                // This will push telemetry data through Application Insights pipeline faster, allowing you to view results immediately.
                builder.AddApplicationInsightsSettings(developerMode: true);
            }

            builder.AddEnvironmentVariables();

            Configuration = builder.Build();
        }
    }
}