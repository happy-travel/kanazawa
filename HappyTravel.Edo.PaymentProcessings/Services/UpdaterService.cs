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
        public UpdaterService(IHostApplicationLifetime applicationLifetime, ILogger<UpdaterService> logger, IHttpClientFactory clientFactory,
            TracerFactory tracerFactory, IOptions<CompletionOptions> completionOptions, IOptions<CancellationOptions> cancellationOptions,
            IOptions<NotificationOptions> needPaymentOptions, IOptions<ChargeOptions> chargeOptions)
        {
            _applicationLifetime = applicationLifetime;
            _logger = logger;
            _notificationOptions = needPaymentOptions.Value;
            _cancellationOptions = cancellationOptions.Value;
            _completionOptions = completionOptions.Value;
            _chargeOptions = chargeOptions.Value;
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
                await ChargePayments(span, stoppingToken);
                await CancelInvalidBookings(span, stoppingToken);
                await SendAgentSummaryReports(span, stoppingToken);
                await SendAdministratorSummaryReports(span, stoppingToken);
                
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
            
            var getUrl = $"{_completionOptions.Url}/{DateTime.UtcNow:o}";
            await ProcessBookings(getUrl, _completionOptions.Url, _completionOptions.ChunkSize, nameof(CapturePayments), stoppingToken);
        }


        private async Task ChargePayments(TelemetrySpan parentSpan, CancellationToken stoppingToken)
        {
            using var scope = _tracer.StartActiveSpan($"{nameof(UpdaterService)}/{nameof(ChargePayments)}", parentSpan, out _);

            var date = DateTime.UtcNow.AddDays(_chargeOptions.DaysBeforeDeadline);

            var getUrl = $"{_chargeOptions.Url}/{date:o}";
            await ProcessBookings(getUrl, _chargeOptions.Url, _chargeOptions.ChunkSize, nameof(ChargePayments), stoppingToken);
        }


        private async Task CancelInvalidBookings(TelemetrySpan parentSpan, CancellationToken stoppingToken)
        {
            using var scope = _tracer.StartActiveSpan($"{nameof(UpdaterService)}/{nameof(CancelInvalidBookings)}", parentSpan, out _);
            
            await ProcessBookings(_cancellationOptions.Url, _cancellationOptions.Url, _completionOptions.ChunkSize, nameof(CancelInvalidBookings), stoppingToken);
        }


        private async Task SendAgentSummaryReports(TelemetrySpan parentSpan, CancellationToken stoppingToken)
        {
            using var scope = _tracer.StartActiveSpan($"{nameof(UpdaterService)}/{nameof(SendAgentSummaryReports)}", parentSpan, out _);
            
            var requestUrl = $"{_notificationOptions.Url}/agent-summary/send";
            await ProcessSingleRequest(requestUrl, nameof(SendAgentSummaryReports), stoppingToken);
        }
        
        
        private async Task SendAdministratorSummaryReports(TelemetrySpan parentSpan, CancellationToken stoppingToken)
        {
            using var scope = _tracer.StartActiveSpan($"{nameof(UpdaterService)}/{nameof(SendAdministratorSummaryReports)}", parentSpan, out _);
            
            var requestUrl = $"{_notificationOptions.Url}/administrator-summary/send";
            await ProcessSingleRequest(requestUrl, nameof(SendAdministratorSummaryReports), stoppingToken);
        }


        private async Task ProcessSingleRequest(string requestUrl, string operationName, CancellationToken stoppingToken)
        {
            using var response = await _client.PostAsync(requestUrl, new StringContent(string.Empty), stoppingToken);
            var message = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogCritical($"Unsuccessful response for operation '{operationName}'. status: {response.StatusCode}. Message: {message}");
            }
            
            _logger.LogInformation($"Processing operation {operationName} finished successfully");
        }
        

        private async Task ProcessBookings(string requestUrl, string processUrl, int chunkSize, string operationName, CancellationToken stoppingToken)
        {
            using var response = await _client.GetAsync(requestUrl, stoppingToken);
            var message = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogCritical($"Unsuccessful response for operation '{operationName}'. status: {response.StatusCode}. Message: {message}");
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
                await Process(forProcess);
            }


            async Task Process(int[] forProcess)
            {
                var json = JsonConvert.SerializeObject(forProcess);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var chunkResponse = await _client.PostAsync(processUrl, content, stoppingToken);
                var chunkMessage = await chunkResponse.Content.ReadAsStringAsync();

                if (chunkResponse.IsSuccessStatusCode)
                {
                    var operationResult = JsonConvert.DeserializeObject<BatchOperationResult>(chunkMessage);
                    if(operationResult.HasErrors)
                        _logger.LogCritical($"{chunkSize} bookings response. status: {chunkResponse.StatusCode}. Message: {operationResult.Message}");
                    else
                        _logger.LogInformation($"{chunkSize} bookings response. status: {chunkResponse.StatusCode}. Message: {operationResult.Message}");
                }

                else
                {
                    _logger.LogCritical($"{chunkSize} bookings response. status: {chunkResponse.StatusCode}. Message: {chunkMessage}");
                }    
            }
        }


        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly CancellationOptions _cancellationOptions;
        private readonly NotificationOptions _notificationOptions;
        private readonly CompletionOptions _completionOptions;
        private readonly ChargeOptions _chargeOptions;
        private readonly ILogger<UpdaterService> _logger;
        private readonly HttpClient _client;
        private readonly Tracer _tracer;
    }
}