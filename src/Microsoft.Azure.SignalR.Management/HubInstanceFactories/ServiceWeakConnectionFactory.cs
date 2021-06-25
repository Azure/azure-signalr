// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceWeakConnectionFactory : IServiceConnectionFactory
    {
        private readonly IServiceProtocol _serviceProtocol;
        private readonly IClientConnectionManager _clientConnectionManager;
        private readonly IConnectionFactory _connectionFactory;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ConnectionDelegate _connectionDelegate;
        private readonly IClientConnectionFactory _clientConnectionFactory;
        private readonly IServerNameProvider _nameProvider;
        private readonly IServiceEventHandler _serviceEventHandler;

        public ServiceWeakConnectionFactory(
            IServiceProtocol serviceProtocol,
            IClientConnectionManager clientConnectionManager,
            IConnectionFactory connectionFactory,
            ILoggerFactory loggerFactory,
            ConnectionDelegate connectionDelegate,
            IClientConnectionFactory clientConnectionFactory,
            IServerNameProvider nameProvider,
            IServiceEventHandler serviceEventHandler)
        {
            _serviceProtocol = serviceProtocol;
            _clientConnectionManager = clientConnectionManager;
            _connectionFactory = connectionFactory;
            _loggerFactory = loggerFactory;
            _connectionDelegate = connectionDelegate;
            _clientConnectionFactory = clientConnectionFactory;
            _nameProvider = nameProvider;
            _serviceEventHandler = serviceEventHandler;
        }

        public virtual IServiceConnection Create(HubServiceEndpoint endpoint, IServiceMessageHandler serviceMessageHandler, ServiceConnectionType type)
        {
            return new ServiceConnection(
                _serviceProtocol,
                _clientConnectionManager,
                _connectionFactory,
                _loggerFactory,
                _connectionDelegate,
                _clientConnectionFactory,
                _nameProvider.GetName(),
                Guid.NewGuid().ToString(),
                endpoint,
                serviceMessageHandler,
                _serviceEventHandler,
                type
            );
        }
    }
}