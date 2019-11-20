using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HappyTravel.Edo.PaymentProcessings.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace HappyTravel.Edo.PaymentProcessings.Services
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

                await CompletePayments(DateTime.UtcNow);

                _applicationLifetime.StopApplication();
            }

            catch (Exception ex)
            {
                _logger.LogCritical(ex, ex.Message);
                _applicationLifetime.StopApplication();
            }
        }


        private async Task CompletePayments(DateTime date)
        {
            using var client = _clientFactory.CreateClient(HttpClientNames.EdoApi);
            var json = JsonConvert.SerializeObject(new ProcessPaymentsInfo(date));
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync("/en/api/1.0/internal/payments/complete", content);
            var message = await response.Content.ReadAsStringAsync();
            _logger.LogInformation($"{response.StatusCode}: {message}");
        }


        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly ILogger<UpdaterService> _logger;
        private readonly IHttpClientFactory _clientFactory;
    }
}
