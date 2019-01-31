using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    class ServiceConnectionFactory : IServiceConnectionFactory
    {
        private readonly IServiceProtocol _serviceProtocol;
        private readonly IClientConnectionManager _clientConnectionManager;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ConnectionDelegate _connectionDelegate;
        private readonly IClientConnectionFactory _clientConnectionFactory;

        public ServiceConnectionFactory(IServiceProtocol serviceProtocol,
            IClientConnectionManager clientConnectionManager,
            ILoggerFactory loggerFactory,
            ConnectionDelegate connectionDelegate,
            IClientConnectionFactory clientConnectionFactory)
        {
            _serviceProtocol = serviceProtocol;
            _clientConnectionManager = clientConnectionManager;
            _loggerFactory = loggerFactory;
            _connectionDelegate = connectionDelegate;
            _clientConnectionFactory = clientConnectionFactory;
        }

        public IServiceConnection Create(IConnectionFactory connectionFactory, IServiceMessageHandler serviceMessageHandler, ServerConnectionType type)
        {
            return new ServiceConnection(_serviceProtocol, _clientConnectionManager, connectionFactory,
                _loggerFactory, _connectionDelegate, _clientConnectionFactory,
                Guid.NewGuid().ToString(), serviceMessageHandler, type);
        }
    }
}
