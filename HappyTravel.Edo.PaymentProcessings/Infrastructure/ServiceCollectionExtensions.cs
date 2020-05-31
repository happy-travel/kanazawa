using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace.Samplers;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace.Configuration;

namespace HappyTravel.Edo.PaymentProcessings.Infrastructure
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddTracing(this IServiceCollection services, IWebHostEnvironment environment, IConfiguration configuration)
        {
            string agentHost;
            int agentPort;
            if (environment.IsLocal())
            {
                agentHost = configuration["Jaeger:AgentHost"];
                agentPort = int.Parse(configuration["Jaeger:AgentPort"]);
            }
            else
            {
                agentHost = "localhost"; //EnvironmentVariableHelper.Get("Jaeger:AgentHost", configuration);
                agentPort = 6831;//int.Parse(EnvironmentVariableHelper.Get("Jaeger:AgentPort", configuration));
            }
            
            var serviceName = $"{environment.ApplicationName}-{environment.EnvironmentName}";
            services.AddOpenTelemetry(builder =>
            {
                builder.UseJaeger(options =>
                    {
                        options.ServiceName = serviceName;
                        options.AgentHost = agentHost;
                        options.AgentPort = agentPort;
                    })
                    .AddRequestAdapter()
                    .AddDependencyAdapter()
                    .SetResource(Resources.CreateServiceResource(serviceName))
                    .SetSampler(new AlwaysOnSampler());
            });

            return services;
        }
    }
}