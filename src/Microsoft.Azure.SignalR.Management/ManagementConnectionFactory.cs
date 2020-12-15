// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ManagementConnectionFactory : IConnectionFactory
    {
        private readonly string _productInfo;
        private readonly ConnectionFactory _connectionFactory;

        public ManagementConnectionFactory(IOptions<ContextOptions> context, ConnectionFactory connectionFactory)
        {
            _productInfo = context.Value.ProductInfo;
            _connectionFactory = connectionFactory;
        }

        public Task<ConnectionContext> ConnectAsync(HubServiceEndpoint endpoint, TransferFormat transferFormat, string connectionId, string target, CancellationToken cancellationToken = default, IDictionary<string, string> headers = null)
        {
            if (headers == null)
            {
                headers = new Dictionary<string, string> { { Constants.AsrsUserAgent, _productInfo } };
            }
            else
            {
                headers[Constants.AsrsUserAgent] = _productInfo;
            }

            return _connectionFactory.ConnectAsync(endpoint, transferFormat, connectionId, target, cancellationToken, headers);
        }

        public Task DisposeAsync(ConnectionContext connection)
        {
            return _connectionFactory.DisposeAsync(connection);
        }
    }
}
