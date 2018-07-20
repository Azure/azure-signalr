// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.AspNet
{
    /// <summary>
    /// TODO: This factory class is responsible for creating, disposing and starting the server-service connections
    /// </summary>
    internal class ConnectionFactory : IConnectionFactory
    {
        private readonly ServiceOptions _options;
        private readonly HubConfiguration _config;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<ConnectionFactory> _logger;
        private readonly IReadOnlyList<string> _hubNames;
        private readonly IServiceConnectionManager _serviceConnectionManager;
        private readonly IServiceProtocol _protocol;
        private readonly IServiceEndpoint _endpoint;
        private readonly string _name;
        private readonly string _userId;

        public ConnectionFactory(IReadOnlyList<string> hubNames, HubConfiguration hubConfig)
        {
            _config = hubConfig;
            _hubNames = hubNames;
            _name = $"{nameof(ConnectionFactory)}[{string.Join(",", hubNames)}]";
            _userId = GenerateServerName();

            _loggerFactory = hubConfig.Resolver.Resolve<ILoggerFactory>() ?? NullLoggerFactory.Instance;
            _protocol = hubConfig.Resolver.Resolve<IServiceProtocol>();
            _serviceConnectionManager = hubConfig.Resolver.Resolve<IServiceConnectionManager>();
            _endpoint = hubConfig.Resolver.Resolve<IServiceEndpoint>();
            _options = hubConfig.Resolver.Resolve<IOptions<ServiceOptions>>().Value;

            _logger = _loggerFactory.CreateLogger<ConnectionFactory>();
        }

        public Task<ConnectionContext> ConnectAsync(TransferFormat transferFormat, string connectionId, string hubName, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task DisposeAsync(ConnectionContext connection)
        {
            throw new NotImplementedException();
        }

        public Task StartAsync()
        {
            throw new NotImplementedException();
        }

        private static string GenerateServerName()
        {
            // Use the machine name for convenient diagnostics, but add a guid to make it unique.
            // Example: MyServerName_02db60e5fab243b890a847fa5c4dcb29
            return $"{Environment.MachineName}_{Guid.NewGuid():N}";
        }
    }
}
