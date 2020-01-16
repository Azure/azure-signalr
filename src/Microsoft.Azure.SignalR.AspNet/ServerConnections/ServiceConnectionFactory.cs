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
        private readonly IServerNameProvider _nameProvider;

        public ServiceConnectionFactory(
            IServiceProtocol serviceProtocol,
            IClientConnectionManager clientConnectionManager,
            IConnectionFactory connectionFactory,
            ILoggerFactory logger,
            IServerNameProvider nameProvider
            )
        {
            _serviceProtocol = serviceProtocol;
            _clientConnectionManager = clientConnectionManager;
            _connectionFactory = connectionFactory;
            _logger = logger;
            _nameProvider = nameProvider;
        }

        public IServiceConnection Create(HubServiceEndpoint endpoint, IServiceMessageHandler serviceMessageHandler, ServiceConnectionType type)
        {
            return new ServiceConnection(_nameProvider.GetName(), Guid.NewGuid().ToString(), endpoint, _serviceProtocol, _connectionFactory, _clientConnectionManager, _logger, serviceMessageHandler, type);
        }
    }
}
