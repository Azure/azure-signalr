// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.Management
{
    internal class RestHealthCheckService : IHostedService
    {
        //internal by test
        //An acceptable time to wait before retry when clients negotiate fail
        internal static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(10);
        internal static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);
        private const int MaxRetries = 2;

        private readonly RestClientFactory _clientFactory;
        private readonly IServiceEndpointManager _serviceEndpointManager;
        private readonly ILogger<RestHealthCheckService> _logger;
        private readonly string _hubName;

        private readonly TimerAwaitable _timer = new(CheckInterval, CheckInterval);

        public RestHealthCheckService(RestClientFactory clientFactory, IServiceEndpointManager serviceEndpointManager, ILogger<RestHealthCheckService> logger, string hubName)
        {
            _clientFactory = clientFactory;
            _serviceEndpointManager = serviceEndpointManager;
            _logger = logger;
            _hubName = hubName;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // wait for the first health check finished
            await CheckEndpointHealthAsync();
            _ = LoopAsync();
        }

        public Task StopAsync(CancellationToken _)
        {
            _timer.Stop();
            return Task.CompletedTask;
        }

        private async Task CheckEndpointHealthAsync()
        {
            await Task.WhenAll(_serviceEndpointManager.GetEndpoints(_hubName).Select(async endpoint =>
            {
                var retry = 0;
                var isHealthy = false;
                bool needRetry;
                do
                {
                    try
                    {
                        using var client = _clientFactory.Create(endpoint);
                        isHealthy = await client.IsServiceHealthy(default);
                    }
                    catch (Exception ex)
                    {
                        //Hard to tell if it is transient error, retry anyway.
                        Log.RestHealthCheckFailed(_logger, endpoint.Endpoint, ex, retry == MaxRetries);
                    }
                    needRetry = !isHealthy && retry < MaxRetries;
                    if (needRetry)
                    {
                        await Task.Delay(RetryInterval);
                    }
                    retry++;
                } while (needRetry);
                endpoint.Online = isHealthy;
            }));
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

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _restHealthCheckFailed = LoggerMessage.Define<string>(LogLevel.Information, new EventId(1, nameof(RestHealthCheckFailed)), "Failed to check health state for endpoint {endpoint}");

            private static readonly Action<ILogger, string, Exception> _restHealthLastCheckFailed = LoggerMessage.Define<string>(LogLevel.Error, new EventId(2, nameof(RestHealthCheckFailed)), "Failed to check health state for endpoint {endpoint}");

            public static void RestHealthCheckFailed(ILogger logger, string endpoint, Exception exception, bool lastRetry)
            {
                if (lastRetry)
                {
                    _restHealthLastCheckFailed(logger, endpoint, exception);
                }
                else
                {
                    _restHealthCheckFailed(logger, endpoint, exception);
                }
            }
        }
    }
}