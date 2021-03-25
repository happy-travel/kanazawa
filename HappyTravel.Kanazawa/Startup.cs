using System;
using HappyTravel.Kanazawa.Infrastructure;
using HappyTravel.Kanazawa.Models;
using HappyTravel.Kanazawa.Services;
using HappyTravel.VaultClient;
using IdentityModel.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace HappyTravel.Kanazawa
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _environment = environment;
            Configuration = configuration;
        }


        public void ConfigureServices(IServiceCollection services)
        {
            var serializationSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Formatting = Formatting.None
            };
            JsonConvert.DefaultSettings = () => serializationSettings;

            using var vaultClient = new VaultClient.VaultClient(new VaultOptions
            {
                Engine = Configuration["Vault:Engine"],
                Role = Configuration["Vault:Role"],
                BaseUrl = new Uri(EnvironmentVariableHelper.Get("Vault:Endpoint", Configuration))
            });
            
            vaultClient.Login(EnvironmentVariableHelper.Get("Vault:Token", Configuration)).Wait();

            var jobsSettings = vaultClient.Get(Configuration["Identity:JobsOptions"]).GetAwaiter().GetResult();
            var clientSecret = jobsSettings[Configuration["Identity:Secret"]];

            var edoSettings = vaultClient.Get(Configuration["Edo:EdoOptions"]).GetAwaiter().GetResult();
            var authorityUrl = edoSettings[Configuration["Identity:Authority"]];
            var edoApiUrl = edoSettings[Configuration["Edo:Api"]];

            services.AddTransient<ProtectedApiBearerTokenHandler>();

            services
                .Configure<CompletionOptions>(Configuration.GetSection("Completion"))
                .Configure<ChargeOptions>(Configuration.GetSection("Charge"))
                .Configure<CancellationOptions>(Configuration.GetSection("Cancellation"))
                .Configure<NotificationOptions>(Configuration.GetSection("Notification"))
                .Configure<MarkupBonusMaterializationOptions>(Configuration.GetSection("MarkupBonusMaterialization"))
                .Configure<ClientCredentialsTokenRequest>(options =>
                {
                    var uri = new Uri(new Uri(authorityUrl), "/connect/token");
                    options.Address = uri.ToString();
                    options.ClientId = Configuration["Identity:ClientId"];
                    options.ClientSecret = clientSecret;
                    options.Scope = "edo";
                });

            services.AddHttpClient(HttpClientNames.Identity, client =>
            {
                client.BaseAddress = new Uri(authorityUrl);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            });
            services.AddHttpClient(HttpClientNames.EdoApi, client =>
            {
                client.BaseAddress = new Uri(edoApiUrl);
                client.Timeout = TimeSpan.FromHours(1);
            }).AddHttpMessageHandler<ProtectedApiBearerTokenHandler>();

            services.AddHealthChecks();
            services.AddTracing(_environment, Configuration);

            services.AddHostedService<UpdaterService>();
        }


        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseHealthChecks("/health");
        }


        public IConfiguration Configuration { get; }
        private readonly IWebHostEnvironment _environment;
    }
}