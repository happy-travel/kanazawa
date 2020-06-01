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
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;

namespace HappyTravel.Edo.PaymentProcessings.Services
{
    public class UpdaterService : BackgroundService
    {
        public UpdaterService(IHostApplicationLifetime applicationLifetime, ILogger<UpdaterService> logger, IHttpClientFactory clientFactory, TracerFactory tracerFactory,
            IOptions<CompletionOptions> completionOptions, IOptions<CancellationOptions> cancellationOptions, IOptions<NotificationOptions> needPaymentOptions)
        {
            _applicationLifetime = applicationLifetime;
            _logger = logger;
            _notificationOptions = needPaymentOptions.Value;
            _cancellationOptions = cancellationOptions.Value;
            _completionOptions = completionOptions.Value;
            _client = clientFactory.CreateClient(HttpClientNames.EdoApi);
            _tracer = tracerFactory.GetTracer(nameof(UpdaterService));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var scope = _tracer.StartActiveSpan($"{nameof(UpdaterService)}/{nameof(ExecuteAsync)}", out var span);

            try
            {
                stoppingToken.ThrowIfCancellationRequested();
                span.AddEvent("Starting booking processing");
                
                await CapturePayments(span, stoppingToken);
                await CancelInvalidBookings(span, stoppingToken);
                await NotifyDeadlineApproaching(span, stoppingToken);
                
                span.AddEvent("Finished booking processing");
                _applicationLifetime.StopApplication();
            }

            catch (Exception ex)
            {
                _logger.LogCritical(ex, ex.Message);
                span.AddEvent($"Failed to process bookings: {ex.Message}");
                span.End();
                _applicationLifetime.StopApplication();
            }
        }


        private async Task CapturePayments(TelemetrySpan parentSpan, CancellationToken stoppingToken)
        {
            using var scope = _tracer.StartActiveSpan($"{nameof(UpdaterService)}/{nameof(CapturePayments)}", parentSpan, out _);
            
            var requestUrl = $"{_completionOptions.Url}/{DateTime.UtcNow:o}";
            await ProcessBookings(requestUrl, _completionOptions.Url, _completionOptions.ChunkSize, nameof(CapturePayments), stoppingToken);
        }


        private async Task CancelInvalidBookings(TelemetrySpan parentSpan, CancellationToken stoppingToken)
        {
            using var scope = _tracer.StartActiveSpan($"{nameof(UpdaterService)}/{nameof(CancelInvalidBookings)}", parentSpan, out _);
            
            var requestUrl = $"{_cancellationOptions.Url}/{DateTime.UtcNow:o}";
            await ProcessBookings(requestUrl, _cancellationOptions.Url, _completionOptions.ChunkSize, nameof(CancelInvalidBookings), stoppingToken);
        }


        private async Task NotifyDeadlineApproaching(TelemetrySpan parentSpan, CancellationToken stoppingToken)
        {
            using var scope = _tracer.StartActiveSpan($"{nameof(UpdaterService)}/{nameof(NotifyDeadlineApproaching)}", parentSpan, out _);
            
            var date = DateTime.UtcNow.AddDays(3);
            var requestUrl = $"{_notificationOptions.Url}/{date:o}";
            await ProcessBookings(requestUrl, _notificationOptions.Url, _completionOptions.ChunkSize, nameof(NotifyDeadlineApproaching), stoppingToken);
        }


        private async Task ProcessBookings(string requestUrl, string processUrl, int chunkSize, string operationName, CancellationToken stoppingToken)
        {
            using var response = await _client.GetAsync(requestUrl, stoppingToken);
            var message = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Unsuccessful response for operation '{operationName}'. status: {response.StatusCode}. Message: {message}");
                return;
            }

            var bookingIds = JsonConvert.DeserializeObject<int[]>(message);
            if (!bookingIds.Any())
            {
                _logger.LogInformation($"There aren't any bookings for '{operationName}'");
                return;
            }

            for (var from = 0; from <= bookingIds.Length; from += _cancellationOptions.ChunkSize)
            {
                var to = Math.Min(from + chunkSize, bookingIds.Length);
                var forProcess = bookingIds[from..to];
                await ProcessBookings(forProcess);
            }


            async Task ProcessBookings(int[] forProcess)
            {
                var json = JsonConvert.SerializeObject(forProcess);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var chunkResponse = await _client.PostAsync(processUrl, content, stoppingToken);
                var chunkMessage = await chunkResponse.Content.ReadAsStringAsync();
                _logger.LogInformation($"{chunkSize} bookings response. status: {chunkResponse.StatusCode}. Message: {chunkMessage}");
            }
        }


        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly CancellationOptions _cancellationOptions;
        private readonly NotificationOptions _notificationOptions;
        private readonly CompletionOptions _completionOptions;
        private readonly ILogger<UpdaterService> _logger;
        private readonly HttpClient _client;
        private readonly Tracer _tracer;
    }
}