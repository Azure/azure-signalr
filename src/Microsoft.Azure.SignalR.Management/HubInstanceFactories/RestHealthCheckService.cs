// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management
{
    internal class RestHealthCheckService : IHostedService
    {
        internal const int MaxRetries = 2;

        //An acceptable time to wait before retry when clients negotiate fail
        private readonly TimeSpan _checkInterval;

        private readonly TimeSpan _httpTimeout;
        private readonly TimeSpan _retryInterval;

        private readonly IServiceEndpointManager _serviceEndpointManager;
        private readonly ILogger<RestHealthCheckService> _logger;
        private readonly string _hubName;

        private readonly TimerAwaitable _timer;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly bool _enabledForSingleEndpoint;

        public RestHealthCheckService(IServiceEndpointManager serviceEndpointManager, ILogger<RestHealthCheckService> logger, string hubName, IOptions<HealthCheckOption> options, IHttpClientFactory httpClientFactory)
        {
            var checkOptions = options.Value;
            _serviceEndpointManager = serviceEndpointManager;
            _logger = logger;
            _hubName = hubName;

            _checkInterval = checkOptions.CheckInterval;
            _retryInterval = checkOptions.RetryInterval;
            _httpTimeout = checkOptions.HttpTimeout;

            _timer = new TimerAwaitable(_checkInterval, _checkInterval);
            _httpClientFactory = httpClientFactory;
            _enabledForSingleEndpoint = checkOptions.EnabledForSingleEndpoint;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // wait for the first health check finished
            await CheckEndpointHealthAsync();
            _ = LoopAsync();
        }

        public Task StopAsync(CancellationToken _)
        {
            _timer?.Stop();
            return Task.CompletedTask;
        }

        private async Task CheckEndpointHealthAsync()
        {
            if (_serviceEndpointManager.Endpoints.Count > 1 || _enabledForSingleEndpoint ||
                // If there is only one unhealthy endpoint, we still need to check its health to give it a chance to become healthy again.
                _serviceEndpointManager.Endpoints.Any(e => !e.Key.Online))
            {
                await Task.WhenAll(_serviceEndpointManager.GetEndpoints(_hubName).Select(async endpoint =>
                {
                    var retry = 0;
                    var isHealthy = false;
                    bool needRetry;
                    do
                    {
                        isHealthy = await IsServiceHealthy(endpoint);
                        needRetry = !isHealthy && retry < MaxRetries;
                        if (needRetry)
                        {
                            Log.WillRetryHealthCheck(_logger, endpoint.Endpoint, _retryInterval);
                            await Task.Delay(_retryInterval);
                        }
                        retry++;
                    } while (needRetry);
                    endpoint.Online = isHealthy;
                    if (!isHealthy)
                    {
                        Log.RestHealthCheckFailed(_logger, endpoint.Endpoint);
                    }
                }));
            }
        }

        private async Task LoopAsync()
        {
            using (_timer)
            {
                _timer.Start();
                while (await _timer)
                {
                    await CheckEndpointHealthAsync();
                }
            }
        }

        private async Task<bool> IsServiceHealthy(ServiceEndpoint endpoint)
        {
            using var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientNames.InternalDefault);
            try
            {
                httpClient.BaseAddress = endpoint.ServerEndpoint;
                httpClient.Timeout = _httpTimeout;
                var request = new HttpRequestMessage(HttpMethod.Head, RestApiProvider.HealthApiPath);
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception ex)
            {
                //Hard to tell if it is transient error, retry anyway.
                Log.RestHealthCheckGetUnexpectedResult(_logger, endpoint.Endpoint, ex);
                return false;
            }
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _restHealthCheckGetUnexpectedResult = LoggerMessage.Define<string>(LogLevel.Information, new EventId(1, nameof(RestHealthCheckGetUnexpectedResult)), "Got unexpected result when checking health of endpoint {endpoint}. It may be transient.");

            private static readonly Action<ILogger, string, Exception> _restHealthyCheckFailed = LoggerMessage.Define<string>(LogLevel.Error, new EventId(2, nameof(RestHealthCheckFailed)), "Service {endpoint} is unhealthy.");

            private static readonly Action<ILogger, string, TimeSpan, Exception> _willRetryHealthCheck = LoggerMessage.Define<string, TimeSpan>(LogLevel.Information, new EventId(3, nameof(RestHealthCheckFailed)), "Will retry health check for endpoint {endpoint} after a delay of {retryInterval} due to exception.");

            public static void WillRetryHealthCheck(ILogger logger, string endpoint, TimeSpan retryInterval)
            {
                _willRetryHealthCheck(logger, endpoint, retryInterval, null);
            }

            public static void RestHealthCheckGetUnexpectedResult(ILogger logger, string endpoint, Exception exception)
            {
                _restHealthCheckGetUnexpectedResult(logger, endpoint, exception);
            }
            public static void RestHealthCheckFailed(ILogger logger, string endpoint)
            {
                _restHealthyCheckFailed(logger, endpoint, null);
            }
        }
    }
}