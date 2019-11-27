using System;
using HappyTravel.Edo.PaymentProcessings.Models;
using HappyTravel.Edo.PaymentProcessings.Services;
using HappyTravel.VaultClient;
using IdentityModel.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace HappyTravel.Edo.PaymentProcessings
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
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

            services.Configure<CompletionOptions>(Configuration.GetSection("Completion"));

            string clientSecret;
            string authorityUrl;
            string edoApiUrl;

            using (var vaultClient = new VaultClient.VaultClient(new VaultOptions
            {
                Engine = Configuration["Vault:Engine"],
                Role = Configuration["Vault:Role"],
                BaseUrl = new Uri(GetFromEnvironment("Vault:Endpoint"))
            }, null))
            {
                vaultClient.Login(GetFromEnvironment("Vault:Token")).Wait();

                var jobsSettings = vaultClient.Get(Configuration["Identity:JobsOptions"]).Result;
                clientSecret = jobsSettings[Configuration["Identity:Secret"]];

                var edoSettings = vaultClient.Get(Configuration["Edo:EdoOptions"]).Result;
                authorityUrl = edoSettings[Configuration["Identity:Authority"]];
                edoApiUrl = edoSettings[Configuration["Edo:Api"]];
            }

            services.AddTransient<ProtectedApiBearerTokenHandler>();
            services.Configure<ClientCredentialsTokenRequest>(options =>
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

            services.AddHostedService<UpdaterService>();
        }


        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseHealthChecks("/health");
        }


        private string GetFromEnvironment(string key)
        {
            var environmentVariable = Configuration[key];
            if (environmentVariable is null)
                throw new Exception($"Couldn't obtain the value for '{key}' configuration key.");

            return Environment.GetEnvironmentVariable(environmentVariable);
        }


        public IConfiguration Configuration { get; }
    }
}