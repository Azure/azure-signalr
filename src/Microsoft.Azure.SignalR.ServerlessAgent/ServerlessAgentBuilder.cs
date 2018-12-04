using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Azure.SignalR.ServerlessAgent
{
    public class ServerlessAgentBuilder
    {
        private AgentContext _context;

        public ServerlessAgentBuilder()
        {
            _context = new AgentContext();
        }

        public ServerlessAgentBuilder WithConnectionString(string connectionString)
        {
            _context.Credentail.SignalrServiceCredential = new SignalRServiceCredential(connectionString);
            return this;
        }

        public ServerlessAgentBuilder WithEndpoint(string endpoint)
        {
            _context.Endpoint = endpoint;
            return this;
        }

        public ServerlessAgentBuilder WithAccessToken(string accessToken)
        {
            _context.Credentail.AddAccessToken(accessToken);
            return this;
        }

        public ServerlessAgentBuilder WithAccessTokens(IList<string> accessTokens)
        {
            accessTokens.ToList().ForEach(token => WithAccessToken(token));
            return this;
        }

        public ServerlessAgentBuilder UseRestV1()
        {
            _context.Backend = Backend.RestApi;
            _context.RestApiVersion = RestApiVersions.V1;
            return this;
        }

        public ServerlessAgentBuilder UseWebsocket()
        {
            _context.Backend = Backend.Websocket;
            return this;
        }

        public IHubContext<Hub> BuildAsync(string hubName)
        {
            _context.HubName = hubName;

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSignalRCore();

            // remove default hub lifetime manager
            var serviceDescriptor = serviceCollection.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(HubLifetimeManager<>));
            serviceCollection.Remove(serviceDescriptor);

            switch(_context.Backend)
            {
                case Backend.RestApi:
                    serviceCollection.AddSingleton(typeof(HubLifetimeManager<Hub>), new RestHubLifetimeManager<Hub>(_context));
                    break;
                case Backend.Websocket:
                    serviceCollection.AddSingleton(new WebsocketHubLifetimeManager<Hub>());
                    break;
            }

            var services = serviceCollection.BuildServiceProvider();

            var hubContext = services.GetService<IHubContext<Hub>>();

            return hubContext;
        }
    }
}
