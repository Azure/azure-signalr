using System;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
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

        private readonly IServiceEventHandler _serviceEventHandler;

        private readonly IClientInvocationManager _clientInvocationManager;

        private readonly IHubProtocolResolver _hubProtocolResolver;

        public GracefulShutdownMode ShutdownMode { get; set; } = GracefulShutdownMode.Off;

        public bool AllowStatefulReconnects { get; set; }

        public Action<HttpContext> ConfigureContext { get; set; }

        public ServiceConnectionFactory(
            IServiceProtocol serviceProtocol,
            IClientConnectionManager clientConnectionManager,
            IConnectionFactory connectionFactory,
            ILoggerFactory loggerFactory,
            ConnectionDelegate connectionDelegate,
            IClientConnectionFactory clientConnectionFactory,
            IServerNameProvider nameProvider,
            IServiceEventHandler serviceEventHandler,
            IClientInvocationManager clientInvocationManager,
            IHubProtocolResolver hubProtocolResolver)
        {
            _serviceProtocol = serviceProtocol;
            _clientConnectionManager = clientConnectionManager;
            _connectionFactory = connectionFactory;
            _loggerFactory = loggerFactory;
            _connectionDelegate = connectionDelegate;
            _clientConnectionFactory = clientConnectionFactory;
            _nameProvider = nameProvider;
            _serviceEventHandler = serviceEventHandler;
            _clientInvocationManager = clientInvocationManager;
            _hubProtocolResolver = hubProtocolResolver;
        }

        public virtual IServiceConnection Create(HubServiceEndpoint endpoint, IServiceMessageHandler serviceMessageHandler, AckHandler ackHandler, ServiceConnectionType type)
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
                _clientInvocationManager,
                _hubProtocolResolver,
                type,
                ShutdownMode,
                allowStatefulReconnects: AllowStatefulReconnects
            )
            {
                ConfigureContext = ConfigureContext
            };
        }
    }
}
