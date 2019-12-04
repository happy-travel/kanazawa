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
            IOptions<CompletionOptions> completionOptions, IOptions<CancellationOptions> cancellationOptions)
        {
            _applicationLifetime = applicationLifetime;
            _logger = logger;
            _clientFactory = clientFactory;
            _cancellationOptions = cancellationOptions.Value;
            _completionOptions = completionOptions.Value;
        }


        private HttpClient Client => _client ??= _clientFactory.CreateClient(HttpClientNames.EdoApi);


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                stoppingToken.ThrowIfCancellationRequested();

                await Task.WhenAll(
                    CancelPayments(stoppingToken),
                    CompletePayments(stoppingToken)
                );

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
            using var response = await Client.GetAsync($"{_completionOptions.Url}/{date:o}", stoppingToken);
            var message = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Unsuccessful response. status: {response.StatusCode}. Message: {message}");
                return;
            }

            var model = JsonConvert.DeserializeObject<ListOfBookingIds>(message);
            if (!model.BookingIds.Any())
            {
                _logger.LogInformation("There aren't any bookings for completion");
                return;
            }

            for (var from = 0; from <= model.BookingIds.Length; from += _completionOptions.ChunkSize)
            {
                var to = Math.Min(from + _completionOptions.ChunkSize, model.BookingIds.Length);
                var forProcess = model.BookingIds[from..to];
                await ProcessBookings(forProcess);
            }


            async Task ProcessBookings(int[] bookingIds)
            {
                var chunkModel = new ListOfBookingIds(bookingIds);
                var json = JsonConvert.SerializeObject(chunkModel);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var chunkResponse = await Client.PostAsync($"{_completionOptions.Url}", content, stoppingToken);
                var chunkMessage = await chunkResponse.Content.ReadAsStringAsync();
                _logger.LogInformation($"Process bookings response. status: {chunkResponse.StatusCode}. Message: {chunkMessage}");
            }
        }


        // TODO: move to separate repository
        private async Task CancelPayments(CancellationToken stoppingToken)
        {
            var date = DateTime.UtcNow;
            using var response = await Client.GetAsync($"{_cancellationOptions.Url}/{date:o}", stoppingToken);
            var message = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Unsuccessful response. status: {response.StatusCode}. Message: {message}");
                return;
            }

            var model = JsonConvert.DeserializeObject<ListOfBookingIds>(message);
            if (!model.BookingIds.Any())
            {
                _logger.LogInformation("There aren't any bookings for cancellation");
                return;
            }

            for (var from = 0; from <= model.BookingIds.Length; from += _cancellationOptions.ChunkSize)
            {
                var to = Math.Min(from + _cancellationOptions.ChunkSize, model.BookingIds.Length);
                var forProcess = model.BookingIds[from..to];
                await ProcessBookings(forProcess);
            }


            async Task ProcessBookings(int[] bookingIds)
            {
                var chunkModel = new ListOfBookingIds(bookingIds);
                var json = JsonConvert.SerializeObject(chunkModel);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var chunkResponse = await Client.PostAsync($"{_cancellationOptions.Url}", content, stoppingToken);
                var chunkMessage = await chunkResponse.Content.ReadAsStringAsync();
                _logger.LogInformation($"Process bookings response. status: {chunkResponse.StatusCode}. Message: {chunkMessage}");
            }
        }


        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly CancellationOptions _cancellationOptions;
        private readonly IHttpClientFactory _clientFactory;
        private readonly CompletionOptions _completionOptions;
        private readonly ILogger<UpdaterService> _logger;
        private HttpClient _client;
    }
}