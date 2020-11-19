// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
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
        private readonly string _name;

        public ServiceHubDispatcher(IReadOnlyList<string> hubNames,
            IServiceConnectionManager serviceConnectionManager, 
            IServiceConnectionContainerFactory serviceConnectionContainerFactory,
            IOptions<ServiceOptions> options, 
            ILoggerFactory loggerFactory)
        {
            _hubNames = hubNames;
            _name = $"{nameof(ServiceHubDispatcher)}[{string.Join(",", hubNames)}]";
            _loggerFactory = loggerFactory;
            _serviceConnectionManager = serviceConnectionManager ?? throw new ArgumentNullException(nameof(serviceConnectionManager));
            _serviceConnectionContainerFactory = serviceConnectionContainerFactory;
            _options = options?.Value;
            _logger = _loggerFactory.CreateLogger<ServiceHubDispatcher>();
        }

        public Task StartAsync()
        {
            _serviceConnectionManager.Initialize(_serviceConnectionContainerFactory);

            Log.StartingConnection(_logger, _name, _options.ConnectionCount, _hubNames.Count);

            return _serviceConnectionManager.StartAsync();
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
