// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceHubDispatcher<THub> : IConnectionFactory where THub : Hub
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<ServiceHubDispatcher<THub>> _logger;
        private readonly ServiceOptions _options;
        private readonly IEndpointRouter _serviceEndpointProvider;
        private readonly IServiceConnectionManager<THub> _serviceConnectionManager;
        private readonly IClientConnectionManager _clientConnectionManager;
        private readonly IServiceProtocol _serviceProtocol;
        private readonly IClientConnectionFactory _clientConnectionFactory;
        private readonly string _userId;
        private static readonly string Name = $"ServiceHubDispatcher<{typeof(THub).FullName}>";
        // Fix issue: https://github.com/Azure/azure-signalr/issues/198
        // .NET Framework has restriction about reserved string as the header name like "User-Agent"
        private static Dictionary<string, string> CustomHeader = new Dictionary<string, string> { { "Asrs-User-Agent", ProductInfo.GetProductInfo() } };

        public ServiceHubDispatcher(IServiceProtocol serviceProtocol,
            IServiceConnectionManager<THub> serviceConnectionManager,
            IClientConnectionManager clientConnectionManager,
            IEndpointRouter endpointRouter,
            IOptions<ServiceOptions> options,
            ILoggerFactory loggerFactory,
            IClientConnectionFactory clientConnectionFactory)
        {
            _serviceProtocol = serviceProtocol;
            _serviceConnectionManager = serviceConnectionManager;
            _clientConnectionManager = clientConnectionManager;
            _serviceEndpointProvider = endpointRouter;
            _options = options != null ? options.Value : throw new ArgumentNullException(nameof(options));

            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger<ServiceHubDispatcher<THub>>();
            _clientConnectionFactory = clientConnectionFactory;
            _userId = GenerateServerName();
        }

        private static string GenerateServerName()
        {
            // Use the machine name for convenient diagnostics, but add a guid to make it unique.
            // Example: MyServerName_02db60e5fab243b890a847fa5c4dcb29
            return $"{Environment.MachineName}_{Guid.NewGuid():N}";
        }

        public void Start(ConnectionDelegate connectionDelegate)
        {
            // Simply create a couple of connections which connect to Azure SignalR
            var serviceConnection = new MultiEndpointServiceConnection(endpoint => new ServiceConnectionContainer(() => GetServiceConnection(connectionDelegate, endpoint), _options.ConnectionCount), _options.ConnectionStrings);
            _serviceConnectionManager.AddServiceConnection(serviceConnection);
            
            Log.StartingConnection(_logger, Name, _options.ConnectionCount);
            _ = _serviceConnectionManager.StartAsync();
        }

        private ServiceConnection GetServiceConnection(ConnectionDelegate connectionDelegate, ConnectionEndpoint endpoint)
        {
            return new ServiceConnection(
                _serviceProtocol,
                _clientConnectionManager,
                this,
                _loggerFactory,
                connectionDelegate,
                _clientConnectionFactory,
                Guid.NewGuid().ToString(),
                endpoint);
        }

        private IEnumerable<ConnectionEndpoint> GetServiceEndpoints(ConnectionEndpoint[] endpoints)
        {
            if (_options.ConnectServiceStrategy == null)
            {
                return endpoints;
            }

            return _options.ConnectServiceStrategy(endpoints);
        }

        private Uri GetServiceUrl(string connectionId, IServiceEndpointProvider provider)
        {
            var baseUri = new UriBuilder(provider.GetServerEndpoint<THub>());
            var query = "cid=" + connectionId;
            if (baseUri.Query != null && baseUri.Query.Length > 1)
            {
                baseUri.Query = baseUri.Query.Substring(1) + "&" + query;
            }
            else
            {
                baseUri.Query = query;
            }
            return baseUri.Uri;
        }

        internal class ServiceConnectionWithEndpoint
        {
            public ConnectionEndpoint Endpoint { get; set; }
            public IServiceConnectionContainer Connection { get; set; }
        }

        internal class MultiEndpointServiceConnection : IServiceConnectionContainer
        {
            public ServiceConnectionWithEndpoint[] Connections { get; }

            public MultiEndpointServiceConnection(Func<ConnectionEndpoint, IServiceConnectionContainer> generator, ConnectionEndpoint[] endpoints)
            {
                Connections = endpoints.Select(s => new ServiceConnectionWithEndpoint
                {
                    Connection = generator(s),
                    Endpoint = s
                }).ToArray();
            }

            public Task SendToEndpointAsync(ConnectionEndpoint endpoint, ServiceMessage serviceMessage)
            {
                return Connections.First(s => s.Endpoint == endpoint).Connection.WriteAsync(serviceMessage);
            }

            public Task StartAsync()
            {
                return Task.WhenAll(Connections.Select(s => s.Connection.StartAsync()));
            }

            public Task WriteAsync(ServiceMessage serviceMessage)
            {
                return Task.WhenAll(Connections.Select(s => s.Connection.WriteAsync(serviceMessage)));
            }

            public Task StopAsync()
            {
                return Task.WhenAll(Connections.Select(s => s.Connection.StopAsync()));
            }

            public Task WriteAsync(string partitionKey, ServiceMessage serviceMessage)
            {
                return Task.WhenAll(Connections.Select(s => s.Connection.WriteAsync(partitionKey, serviceMessage)));
            }
        }

        public async Task<ConnectionContext> ConnectAsync(TransferFormat transferFormat, string connectionId, IServiceEndpointProvider provider, CancellationToken cancellationToken = default)
        {
            var httpConnectionOptions = new HttpConnectionOptions
            {
                Url = GetServiceUrl(connectionId, provider),
                AccessTokenProvider = () => Task.FromResult(provider.GenerateServerAccessToken<THub>(_userId)),
                Transports = HttpTransportType.WebSockets,
                SkipNegotiation = true,
                Headers = CustomHeader
            };
            var httpConnection = new HttpConnection(httpConnectionOptions, _loggerFactory);
            try
            {
                await httpConnection.StartAsync(transferFormat);
                return httpConnection;
            }
            catch
            {
                await httpConnection.DisposeAsync();
                throw;
            }
        }

        public Task DisposeAsync(ConnectionContext connection)
        {
            if (connection == null)
            {
                return Task.CompletedTask;
            }
            
            return ((HttpConnection)connection).DisposeAsync();
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, int, Exception> _startingConnection =
                LoggerMessage.Define<string, int>(LogLevel.Debug, new EventId(1, "StartingConnection"), "Staring {name} with {connectionNumber} connections...");

            public static void StartingConnection(ILogger logger, string name, int connectionNumber)
            {
                _startingConnection(logger, name, connectionNumber, null);
            }
        }
    }
}
