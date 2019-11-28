using System;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.AspNet
{
    class ServiceConnectionFactory : IServiceConnectionFactory
    {
        private readonly IServiceProtocol _serviceProtocol;
        private readonly IClientConnectionManager _clientConnectionManager;
        private readonly IConnectionFactory _connectionFactory;
        private readonly ILoggerFactory _logger;
        private readonly ServiceConnectionOptions _options = ServiceConnectionOptions.Default;

        public ServiceConnectionFactory(IServiceProtocol serviceProtocol,
            IClientConnectionManager clientConnectionManager,
            IConnectionFactory connectionFactory,
            ILoggerFactory logger)
        {
            _serviceProtocol = serviceProtocol;
            _clientConnectionManager = clientConnectionManager;
            _connectionFactory = connectionFactory;
            _logger = logger;
        }

        public IServiceConnection Create(HubServiceEndpoint endpoint, IServiceMessageHandler serviceMessageHandler, ServiceConnectionType type)
        {
            ServiceConnectionOptions options = _options;
            if (type != _options.ConnectionType)
            {
                options = _options.Clone();
                options.ConnectionType = type;
            }
            return new ServiceConnection(Guid.NewGuid().ToString(), endpoint, _serviceProtocol, _connectionFactory, _clientConnectionManager, _logger, serviceMessageHandler, options);
        }
    }
}
