using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.AspNet
{
    class ServiceConnectionFactory : IServiceConnectionFactory
    {
        private readonly IServiceProtocol _serviceProtocol;
        private readonly IClientConnectionManager _clientConnectionManager;
        private readonly ILoggerFactory _logger;

        public ServiceConnectionFactory(IServiceProtocol serviceProtocol,
            IClientConnectionManager clientConnectionManager,
            ILoggerFactory logger)
        {
            _serviceProtocol = serviceProtocol;
            _clientConnectionManager = clientConnectionManager;
            _logger = logger;
        }

        public IServiceConnection Create(ServiceEndpoint endpoint, IConnectionFactory connectionFactory, IServiceMessageHandler serviceMessageHandler, ServerConnectionType type)
        {
            return new ServiceConnection(Guid.NewGuid().ToString(), _serviceProtocol, connectionFactory, _clientConnectionManager, _logger, serviceMessageHandler, type);
        }
    }
}
