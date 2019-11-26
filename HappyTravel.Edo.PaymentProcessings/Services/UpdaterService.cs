using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HappyTravel.Edo.PaymentProcessings.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace HappyTravel.Edo.PaymentProcessings.Services
{
    public class UpdaterService : BackgroundService
    {
        public UpdaterService(IHostApplicationLifetime applicationLifetime, ILogger<UpdaterService> logger, IHttpClientFactory clientFactory,
            IOptions<CompletionOptions> options)
        {
            _applicationLifetime = applicationLifetime;
            _logger = logger;
            _clientFactory = clientFactory;
            _options = options.Value;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                stoppingToken.ThrowIfCancellationRequested();

                await CompletePayments(stoppingToken);

                _applicationLifetime.StopApplication();
            }

            catch (Exception ex)
            {
                _logger.LogCritical(ex, ex.Message);
                _applicationLifetime.StopApplication();
            }
        }


        private async Task CompletePayments(CancellationToken stoppingToken)
        {
            var date = DateTime.UtcNow;
            var client = _clientFactory.CreateClient(HttpClientNames.EdoApi);
            using var response = await client.GetAsync($"{_options.Url}/{date:o}", stoppingToken);
            var message = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Unsuccessful response. status: {response.StatusCode}. Message: {message}");
                return;
            }

            var model = JsonConvert.DeserializeObject<CompletePaymentsInfo>(message);
            if (!model.BookingIds.Any())
            {
                _logger.LogInformation("There aren't any bookings for completion");
                return;
            }

            for (var from = 0; from <= model.BookingIds.Length; from += _options.ChunkSize)
            {
                var to = Math.Min(from + _options.ChunkSize, model.BookingIds.Length);
                var forProcess = model.BookingIds[from..to];
                await ProcessBookings(forProcess);
            }


            async Task ProcessBookings(int[] bookingIds)
            {
                var chunkModel = new CompletePaymentsInfo(bookingIds);
                var json = JsonConvert.SerializeObject(chunkModel);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var chunkResponse = await client.PostAsync($"{_options.Url}", content, stoppingToken);
                var chunkMessage = await chunkResponse.Content.ReadAsStringAsync();
                _logger.LogInformation($"Process bookings response. status: {chunkResponse.StatusCode}. Message: {chunkMessage}");
            }
        }


        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly IHttpClientFactory _clientFactory;
        private readonly ILogger<UpdaterService> _logger;
        private readonly CompletionOptions _options;
    }
}