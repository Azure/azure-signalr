// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class ServiceHubDispatcher
    {
        private readonly ServiceOptions _options;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<ServiceHubDispatcher> _logger;
        private readonly IReadOnlyList<string> _hubNames;
        private readonly IServiceConnectionManager _serviceConnectionManager;
        private readonly IServiceConnectionContainerFactory _serviceConnectionContainerFactory;
        private readonly IClientConnectionManager _clientConnectionManager;
        private readonly string _name;

        public ServiceHubDispatcher(
            IReadOnlyList<string> hubNames,
            IServiceConnectionManager serviceConnectionManager, 
            IServiceConnectionContainerFactory serviceConnectionContainerFactory,
            IServerLifetimeManager serverLifetimeManager,
            IClientConnectionManager clientConnectionManager,
            IOptions<ServiceOptions> options, 
            ILoggerFactory loggerFactory)
        {
            _hubNames = hubNames;
            _name = $"{nameof(ServiceHubDispatcher)}[{string.Join(",", hubNames)}]";

            _serviceConnectionManager = serviceConnectionManager ?? throw new ArgumentNullException(nameof(serviceConnectionManager));
            _clientConnectionManager = clientConnectionManager ?? throw new ArgumentNullException(nameof(clientConnectionManager));
            _serviceConnectionContainerFactory = serviceConnectionContainerFactory;
            _options = options?.Value;

            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<ServiceHubDispatcher>();

            serverLifetimeManager?.Register(ShutdownAsync);
        }

        public Task StartAsync()
        {
            _serviceConnectionManager.Initialize(_serviceConnectionContainerFactory);

            Log.StartingConnection(_logger, _name, _options.ConnectionCount, _hubNames.Count);

            return _serviceConnectionManager.StartAsync();
        }

        public async Task ShutdownAsync()
        {
            if (_options.GracefulShutdown.Mode == GracefulShutdownMode.Off)
            {
                return;
            }

            using CancellationTokenSource source = new CancellationTokenSource();

            var expected = OfflineAndWaitForCompletedAsync(_options.GracefulShutdown.Mode);
            var actual = await Task.WhenAny(
                Task.Delay(_options.GracefulShutdown.Timeout, source.Token), expected
            );

            if (actual != expected)
            {
                // TODO log timeout.
            }

            source.Cancel();
            await _serviceConnectionManager.StopAsync();
        }

        private async Task OfflineAndWaitForCompletedAsync(GracefulShutdownMode mode)
        {
            await _serviceConnectionManager.OfflineAsync(mode);
            await _clientConnectionManager.WhenAllCompleted();
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, int, int, Exception> _startingConnection =
                LoggerMessage.Define<string, int, int>(LogLevel.Debug, new EventId(1, "StartingConnection"), "Starting {name} with {hubCount} hubs and {connectionCount} per hub connections...");

            public static void StartingConnection(ILogger logger, string name, int connectionCount, int hubCount)
            {
                _startingConnection(logger, name, connectionCount, hubCount, null);
            }
        }
    }
}
