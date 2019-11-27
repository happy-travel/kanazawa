using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HappyTravel.Edo.PaymentProcessings
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            await CreateHostBuilder(args)
                .Build()
                .RunAsync();
        }


        public static IHostBuilder CreateHostBuilder(string[] args)
            => Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .UseKestrel()
                        .UseStartup<Startup>()
                        .UseSentry(options => { options.Dsn = Environment.GetEnvironmentVariable("HTDC_EDO_SENTRY_ENDPOINT"); });
                })
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    var environment = hostingContext.HostingEnvironment;

                    config.AddJsonFile("appsettings.json", false, true)
                        .AddJsonFile($"appsettings.{environment.EnvironmentName}.json", true, true);
                    config.AddEnvironmentVariables();
                })
                .ConfigureLogging((hostingContext, logging) =>
                {
                    logging
                        .AddConfiguration(hostingContext.Configuration.GetSection("Logging"));

                    var env = hostingContext.HostingEnvironment;
                    if (env.IsDevelopment())
                        logging.AddConsole();
                    else
                        logging.AddEventSourceLogger();
                });
    }
}