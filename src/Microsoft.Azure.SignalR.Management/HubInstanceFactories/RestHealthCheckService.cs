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
    internal class RestHealthCheckService : IDisposable, IHostedService
    {
        private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(2);
        // CheckHealthTimeout is a little shorter than CheckInterval, make sure before each check start, the last check finish.
        private static readonly TimeSpan CheckHealthTimeout = TimeSpan.FromSeconds(110);

        private readonly RestClientFactory _clientFactory;
        private readonly IServiceEndpointManager _serviceEndpointManager;
        private readonly ILogger<RestHealthCheckService> _logger;
        private readonly string _hubName;

        private readonly TaskCompletionSource<bool> _firstCheckCompletionSource = new();
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly CancellationToken _cancellationToken;
        private Timer _timer;

        public RestHealthCheckService(RestClientFactory clientFactory, IServiceEndpointManager serviceEndpointManager, ILogger<RestHealthCheckService> logger, string hubName)
        {
            _clientFactory = clientFactory;
            _serviceEndpointManager = serviceEndpointManager;
            _logger = logger;
            _hubName = hubName;
            _cancellationTokenSource = new();
            _cancellationToken = _cancellationTokenSource.Token;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer = new Timer(CheckEndpointHealthAsync, null, TimeSpan.Zero, CheckInterval);
            return _firstCheckCompletionSource.Task.OrTimeout(cancellationToken);
        }

        public Task StopAsync(CancellationToken _)
        {
            _cancellationTokenSource.Cancel();
            _firstCheckCompletionSource.TrySetCanceled();
            return Task.CompletedTask;
        }

        private async void CheckEndpointHealthAsync(object _)
        {
            var endpoints = _serviceEndpointManager.GetEndpoints(_hubName);
            await Task.WhenAll(_serviceEndpointManager.GetEndpoints(_hubName).Select(async endpoint =>
            {
                try
                {
                    using var client = _clientFactory.Create(endpoint);
                    var isHealthy = await client.IsServiceHealthy(_cancellationToken);
                    endpoint.Online = isHealthy;
                }
                catch (OperationCanceledException ex) when (ex.CancellationToken == _cancellationToken)
                {
                    // the health checker is stopping.
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Check health status failed for endpoint {endpoint}", endpoint.Endpoint);
                    endpoint.Online = false;
                }
                finally
                {
                    _firstCheckCompletionSource.TrySetResult(true);
                }
            })).OrTimeout(_cancellationToken, CheckHealthTimeout, nameof(CheckEndpointHealthAsync));
        }

        public void Dispose()
        {
            _timer.Dispose();
        }
    }
}