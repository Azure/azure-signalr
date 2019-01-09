// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceConnectionFactory : IConnectionFactory
    {
        // Fix issue: https://github.com/Azure/azure-signalr/issues/198
        // .NET Framework has restriction about reserved string as the header name like "User-Agent"
        private static readonly Dictionary<string, string> CustomHeader = new Dictionary<string, string> { { "Asrs-User-Agent", ProductInfo.GetProductInfo() } };

        private readonly IServiceEndpointProvider _provider;
        private readonly ILoggerFactory _loggerFactory;
        private readonly string _userId;
        private readonly string _hubName;

        public ServiceConnectionFactory(string hubName, IServiceEndpointProvider provider, ILoggerFactory loggerFactory)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _loggerFactory = loggerFactory;
            _userId = GenerateServerName();
            _hubName = hubName;
        }

        public async Task<ConnectionContext> ConnectAsync(TransferFormat transferFormat, string connectionId, CancellationToken cancellationToken = default)
        {
            var httpConnectionOptions = new HttpConnectionOptions
            {
                Url = GetServiceUrl(connectionId),
                AccessTokenProvider = () => Task.FromResult(_provider.GenerateServerAccessToken(_hubName, _userId)),
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

        private Uri GetServiceUrl(string connectionId)
        {
            var baseUri = new UriBuilder(_provider.GetServerEndpoint(_hubName));
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

        public Task DisposeAsync(ConnectionContext connection)
        {
            if (connection == null)
            {
                return Task.CompletedTask;
            }

            return ((HttpConnection)connection).DisposeAsync();
        }

        private static string GenerateServerName()
        {
            // Use the machine name for convenient diagnostics, but add a guid to make it unique.
            // Example: MyServerName_02db60e5fab243b890a847fa5c4dcb29
            return $"{Environment.MachineName}_{Guid.NewGuid():N}";
        }
    }
}
