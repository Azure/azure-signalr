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
        private readonly IServerNameProvider _nameProvider;

        public GracefulShutdownMode ShutdownMode { get; set; } = GracefulShutdownMode.Off;

        public Action<HttpContext> ConfigureContext { get; set; }

        public ServiceConnectionFactory(
            IServiceProtocol serviceProtocol,
            IClientConnectionManager clientConnectionManager,
            IConnectionFactory connectionFactory,
            ILoggerFactory loggerFactory,
            ConnectionDelegate connectionDelegate,
            IClientConnectionFactory clientConnectionFactory,
            IServerNameProvider nameProvider)
        {
            _serviceProtocol = serviceProtocol;
            _clientConnectionManager = clientConnectionManager;
            _connectionFactory = connectionFactory;
            _loggerFactory = loggerFactory;
            _connectionDelegate = connectionDelegate;
            _clientConnectionFactory = clientConnectionFactory;
            _nameProvider = nameProvider;
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
                type,
                ShutdownMode
            )
            {
                ConfigureContext = ConfigureContext
            };
        }
    }
}
