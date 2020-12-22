using System;
using System.Diagnostics;
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
            IOptions<CompletionOptions> completionOptions, IOptions<CancellationOptions> cancellationOptions,
            IOptions<NotificationOptions> needPaymentOptions, IOptions<ChargeOptions> chargeOptions, 
            IOptions<MarkupBonusMaterializationOptions> markupBonusOptions)
        {
            _applicationLifetime = applicationLifetime;
            _logger = logger;
            _notificationOptions = needPaymentOptions.Value;
            _cancellationOptions = cancellationOptions.Value;
            _completionOptions = completionOptions.Value;
            _chargeOptions = chargeOptions.Value;
            _markupBonusOptions = markupBonusOptions.Value;
            _client = clientFactory.CreateClient(HttpClientNames.EdoApi);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var activity = _activitySource.StartActivity($"{nameof(ExecuteAsync)}");

            try
            {
                stoppingToken.ThrowIfCancellationRequested();
                
                await CapturePayments(activity, stoppingToken);
                await ChargePayments(activity, stoppingToken);
                await CancelInvalidBookings(activity, stoppingToken);
                await SendAgentSummaryReports(activity, stoppingToken);
                await SendAdministratorSummaryReports(activity, stoppingToken);
                await SendAdministratorPaymentsSummaryReports(activity, stoppingToken);
                await MaterializeMarkupBonuses(activity, stoppingToken);
                
                activity?.AddTag("state", "Finished booking processing");
                _applicationLifetime.StopApplication();
            }

            catch (Exception ex)
            {
                _logger.LogCritical(ex, ex.Message);
                activity?.AddTag("state", $"Failed to process bookings: {ex.Message}");
                _applicationLifetime.StopApplication();
            }
        }


        private async Task CapturePayments(Activity? parentActivity, CancellationToken stoppingToken)
        {
            using var activity = parentActivity is not null 
                ? _activitySource.StartActivity($"{nameof(CapturePayments)}", ActivityKind.Internal, parentActivity.Context)
                : _activitySource.StartActivity($"{nameof(CapturePayments)}");
            
            var getUrl = $"{_completionOptions.Url}/{DateTime.UtcNow:o}";
            await ProcessBookings(getUrl, _completionOptions.Url, _completionOptions.ChunkSize, nameof(CapturePayments), stoppingToken);
        }


        private async Task ChargePayments(Activity? parentActivity, CancellationToken stoppingToken)
        {
            using var activity = parentActivity is not null 
                ? _activitySource.StartActivity($"{nameof(ChargePayments)}", ActivityKind.Internal, parentActivity.Context)
                : _activitySource.StartActivity($"{nameof(ChargePayments)}");

            var date = DateTime.UtcNow.AddDays(_chargeOptions.DaysBeforeDeadline);

            var getUrl = $"{_chargeOptions.Url}/{date:o}";
            await ProcessBookings(getUrl, _chargeOptions.Url, _chargeOptions.ChunkSize, nameof(ChargePayments), stoppingToken);
        }


        private async Task CancelInvalidBookings(Activity? parentActivity, CancellationToken stoppingToken)
        {
            using var activity = parentActivity is not null 
                ? _activitySource.StartActivity($"{nameof(CancelInvalidBookings)}", ActivityKind.Internal, parentActivity.Context)
                : _activitySource.StartActivity($"{nameof(CancelInvalidBookings)}");
            
            await ProcessBookings(_cancellationOptions.Url, _cancellationOptions.Url, _completionOptions.ChunkSize, nameof(CancelInvalidBookings), stoppingToken);
        }


        private async Task SendAgentSummaryReports(Activity? parentActivity, CancellationToken stoppingToken)
        {
            using var activity = parentActivity is not null 
                ? _activitySource.StartActivity($"{nameof(SendAgentSummaryReports)}", ActivityKind.Internal, parentActivity.Context)
                : _activitySource.StartActivity($"{nameof(SendAgentSummaryReports)}");
            
            var requestUrl = $"{_notificationOptions.Url}/agent-summary/send";
            await ProcessSingleRequest(requestUrl, nameof(SendAgentSummaryReports), stoppingToken);
        }
        
        
        private async Task SendAdministratorSummaryReports(Activity? parentActivity, CancellationToken stoppingToken)
        {
            using var activity = parentActivity is not null 
                ? _activitySource.StartActivity($"{nameof(SendAdministratorSummaryReports)}", ActivityKind.Internal, parentActivity.Context)
                : _activitySource.StartActivity($"{nameof(SendAdministratorSummaryReports)}");
            
            var requestUrl = $"{_notificationOptions.Url}/administrator-summary/send";
            await ProcessSingleRequest(requestUrl, nameof(SendAdministratorSummaryReports), stoppingToken);
        }
        
        
        private async Task SendAdministratorPaymentsSummaryReports(Activity? parentActivity, CancellationToken stoppingToken)
        {
            using var activity = parentActivity is not null 
                ? _activitySource.StartActivity($"{nameof(SendAdministratorPaymentsSummaryReports)}", ActivityKind.Internal, parentActivity.Context)
                : _activitySource.StartActivity($"{nameof(SendAdministratorPaymentsSummaryReports)}");
            
            var requestUrl = $"{_notificationOptions.Url}/administrator-payment-summary/send";
            await ProcessSingleRequest(requestUrl, nameof(SendAdministratorPaymentsSummaryReports), stoppingToken);
        }

        
        private async Task ProcessSingleRequest(string requestUrl, string operationName, CancellationToken stoppingToken)
        {
            using var response = await _client.PostAsync(requestUrl, new StringContent(string.Empty), stoppingToken);
            var message = await response.Content.ReadAsStringAsync(stoppingToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogCritical($"Unsuccessful response for operation '{operationName}'. status: {response.StatusCode}. Message: {message}");
                return;
            }
            
            _logger.LogInformation($"Processing operation {operationName} finished successfully");
        }
        

        private async Task ProcessBookings(string requestUrl, string processUrl, int chunkSize, string operationName, CancellationToken stoppingToken)
        {
            using var response = await _client.GetAsync(requestUrl, stoppingToken);
            var message = await response.Content.ReadAsStringAsync(stoppingToken);
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
                var chunkMessage = await chunkResponse.Content.ReadAsStringAsync(stoppingToken);

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


        private async Task MaterializeMarkupBonuses(Activity? parentActivity, CancellationToken stoppingToken)
        {
            using var activity = parentActivity is not null 
                ? _activitySource.StartActivity($"{nameof(MaterializeMarkupBonuses)}", ActivityKind.Internal, parentActivity.Context)
                : _activitySource.StartActivity($"{nameof(MaterializeMarkupBonuses)}");
            
            var getUrl = $"{_markupBonusOptions.Url}/{DateTime.UtcNow:o}";
            await ProcessBookings(getUrl, _markupBonusOptions.Url, _markupBonusOptions.ChunkSize, nameof(MaterializeMarkupBonuses), stoppingToken);
        }
        
        
        private readonly ActivitySource _activitySource = new(nameof(UpdaterService));
        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly CancellationOptions _cancellationOptions;
        private readonly NotificationOptions _notificationOptions;
        private readonly CompletionOptions _completionOptions;
        private readonly ChargeOptions _chargeOptions;
        private readonly MarkupBonusMaterializationOptions _markupBonusOptions;
        private readonly ILogger<UpdaterService> _logger;
        private readonly HttpClient _client;
    }
}