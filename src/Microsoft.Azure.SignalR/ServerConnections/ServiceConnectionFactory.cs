using System;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceConnectionFactory : IServiceConnectionFactory
    {
        private readonly IServiceProtocol _serviceProtocol;
        private readonly IClientConnectionManager _clientConnectionManager;
        private readonly IConnectionFactory _connectionFactory;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ConnectionDelegate _connectionDelegate;
        private readonly IClientConnectionFactory _clientConnectionFactory;

        private readonly ServiceConnectionOptions _options;

        public Action<HttpContext> ConfigureContext { get; set; }

        public ServiceConnectionFactory(IServiceProtocol serviceProtocol,
            IClientConnectionManager clientConnectionManager,
            IConnectionFactory connectionFactory,
            ILoggerFactory loggerFactory,
            ConnectionDelegate connectionDelegate,
            IClientConnectionFactory clientConnectionFactory,
            ServiceConnectionOptions options
        )
        {
            _serviceProtocol = serviceProtocol;
            _clientConnectionManager = clientConnectionManager;
            _connectionFactory = connectionFactory;
            _loggerFactory = loggerFactory;
            _connectionDelegate = connectionDelegate;
            _clientConnectionFactory = clientConnectionFactory;
            _options = options;
        }

        public IServiceConnection Create(
            HubServiceEndpoint endpoint,
            IServiceMessageHandler serviceMessageHandler,
            ServiceConnectionType type
        )
        {
            ServiceConnectionOptions options = _options;
            if (type != _options.ConnectionType)
            {
                options = _options.Clone();
                options.ConnectionType = type;
            }
            return new ServiceConnection(
                _serviceProtocol,
                _clientConnectionManager,
                _connectionFactory,
                _loggerFactory,
                _connectionDelegate,
                _clientConnectionFactory,
                Guid.NewGuid().ToString(),
                endpoint,
                serviceMessageHandler,
                options
            )
            {
                ConfigureContext = ConfigureContext
            };
        }
    }
}
