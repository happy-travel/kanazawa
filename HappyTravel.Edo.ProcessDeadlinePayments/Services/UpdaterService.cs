using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HappyTravel.Edo.ProcessDeadlinePayments.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HappyTravel.Edo.ProcessDeadlinePayments.Services
{
    public class UpdaterService : BackgroundService
    {
        public UpdaterService(IHostApplicationLifetime applicationLifetime, ILogger<UpdaterService> logger, IHttpClientFactory clientFactory)
        {
            _applicationLifetime = applicationLifetime;
            _logger = logger;
            _clientFactory = clientFactory;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                stoppingToken.ThrowIfCancellationRequested();

                await ProcessPaymentsOnDeadline(DateTime.UtcNow);

                _applicationLifetime.StopApplication();
            }

            catch (Exception ex)
            {
                _logger.LogCritical(ex, ex.Message);
                _applicationLifetime.StopApplication();
            }
        }


        private async Task ProcessPaymentsOnDeadline(DateTime date)
        {
            using var client = _clientFactory.CreateClient(HttpClientNames.EdoApi);
            // TODO: For test only. Should calls ProcessPaymentsOnDeadline endpoint
            using var response = await client.GetAsync("/en/api/1/payments/methods");
            var message = await response.Content.ReadAsStringAsync();
            _logger.LogInformation($"{response.StatusCode}: {message}");
        }


        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly ILogger<UpdaterService> _logger;
        private readonly IHttpClientFactory _clientFactory;
    }
}
