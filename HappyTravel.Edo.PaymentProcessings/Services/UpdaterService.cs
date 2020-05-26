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
            IOptions<CompletionOptions> completionOptions, IOptions<CancellationOptions> cancellationOptions, IOptions<NeedPaymentOptions> needPaymentOptions)
        {
            _applicationLifetime = applicationLifetime;
            _logger = logger;
            _needPaymentOptions = needPaymentOptions.Value;
            _cancellationOptions = cancellationOptions.Value;
            _completionOptions = completionOptions.Value;
            _client = clientFactory.CreateClient(HttpClientNames.EdoApi);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                stoppingToken.ThrowIfCancellationRequested();

                await Task.WhenAll(
                    CancelPayments(stoppingToken),
                    CapturePayments(stoppingToken),
                    NotifyNeedPayments(stoppingToken)
                );

                _applicationLifetime.StopApplication();
            }

            catch (Exception ex)
            {
                _logger.LogCritical(ex, ex.Message);
                _applicationLifetime.StopApplication();
            }
        }


        private async Task CapturePayments(CancellationToken stoppingToken)
        {
            var date = DateTime.UtcNow;
            using var response = await _client.GetAsync($"{_completionOptions.Url}/{date:o}", stoppingToken);
            var message = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Unsuccessful response. status: {response.StatusCode}. Message: {message}");
                return;
            }

            var bookingIds = JsonConvert.DeserializeObject<int[]>(message);
            if (!bookingIds.Any())
            {
                _logger.LogInformation("There aren't any bookings for capture");
                return;
            }

            for (var from = 0; from <= bookingIds.Length; from += _completionOptions.ChunkSize)
            {
                var to = Math.Min(from + _completionOptions.ChunkSize, bookingIds.Length);
                var forProcess = bookingIds[from..to];
                await ProcessBookings(forProcess);
            }


            async Task ProcessBookings(int[] forProcess)
            {
                var json = JsonConvert.SerializeObject(forProcess);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var chunkResponse = await _client.PostAsync($"{_completionOptions.Url}", content, stoppingToken);
                var chunkMessage = await chunkResponse.Content.ReadAsStringAsync();
                _logger.LogInformation($"Capture bookings response. status: {chunkResponse.StatusCode}. Message: {chunkMessage}");
            }
        }


        // TODO: move to separate repository
        private async Task CancelPayments(CancellationToken stoppingToken)
        {
            var date = DateTime.UtcNow;
            using var response = await _client.GetAsync($"{_cancellationOptions.Url}/{date:o}", stoppingToken);
            var message = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Unsuccessful response. status: {response.StatusCode}. Message: {message}");
                return;
            }

            var bookingIds = JsonConvert.DeserializeObject<int[]>(message);
            if (!bookingIds.Any())
            {
                _logger.LogInformation("There aren't any bookings for cancellation");
                return;
            }

            for (var from = 0; from <= bookingIds.Length; from += _cancellationOptions.ChunkSize)
            {
                var to = Math.Min(from + _cancellationOptions.ChunkSize, bookingIds.Length);
                var forProcess = bookingIds[from..to];
                await ProcessBookings(forProcess);
            }


            async Task ProcessBookings(int[] forProcess)
            {
                var json = JsonConvert.SerializeObject(forProcess);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var chunkResponse = await _client.PostAsync($"{_cancellationOptions.Url}", content, stoppingToken);
                var chunkMessage = await chunkResponse.Content.ReadAsStringAsync();
                _logger.LogInformation($"Cancel bookings response. status: {chunkResponse.StatusCode}. Message: {chunkMessage}");
            }
        }


        // TODO: move to separate repository
        private async Task NotifyNeedPayments(CancellationToken stoppingToken)
        {
            var date = DateTime.UtcNow.AddDays(3);
            using var response = await _client.GetAsync($"{_needPaymentOptions.GetUrl}/{date:o}", stoppingToken);
            var message = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Unsuccessful response. status: {response.StatusCode}. Message: {message}");
                return;
            }

            var bookingIds = JsonConvert.DeserializeObject<int[]>(message);
            if (!bookingIds.Any())
            {
                _logger.LogInformation("There aren't any bookings for cancellation");
                return;
            }

            for (var from = 0; from <= bookingIds.Length; from += _cancellationOptions.ChunkSize)
            {
                var to = Math.Min(from + _cancellationOptions.ChunkSize, bookingIds.Length);
                var forProcess = bookingIds[from..to];
                await ProcessBookings(forProcess);
            }


            async Task ProcessBookings(int[] forProcess)
            {
                var json = JsonConvert.SerializeObject(forProcess);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var chunkResponse = await _client.PostAsync($"{_needPaymentOptions.ProcessUrl}", content, stoppingToken);
                var chunkMessage = await chunkResponse.Content.ReadAsStringAsync();
                _logger.LogInformation($"Cancel bookings response. status: {chunkResponse.StatusCode}. Message: {chunkMessage}");
            }
        }


        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly CancellationOptions _cancellationOptions;
        private readonly NeedPaymentOptions _needPaymentOptions;
        private readonly CompletionOptions _completionOptions;
        private readonly ILogger<UpdaterService> _logger;
        private readonly HttpClient _client;
    }
}