// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ManagementConnectionFactory : IConnectionFactory
    {
        private readonly string _productInfo;
        private readonly IConnectionFactory _connectionFactory;

        public ManagementConnectionFactory(string productInfo, IConnectionFactory connectionFactory)
        {
            _productInfo = productInfo;
            _connectionFactory = connectionFactory;
        }

        public Task<ConnectionContext> ConnectAsync(TransferFormat transferFormat, string connectionId, string target, CancellationToken cancellationToken = default, IDictionary<string, string> headers = null)
        {
            if (headers == null)
            {
                headers = new Dictionary<string, string> { { Constants.AsrsUserAgent, _productInfo } };
            }
            else
            {
                headers[Constants.AsrsUserAgent] = _productInfo;
            }

            return _connectionFactory.ConnectAsync(transferFormat, connectionId, target, cancellationToken, headers);
        }

        public Task DisposeAsync(ConnectionContext connection)
        {
            return _connectionFactory.DisposeAsync(connection);
        }
    }
}
