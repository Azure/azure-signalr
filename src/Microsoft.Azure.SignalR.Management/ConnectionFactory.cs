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
    internal class ConnectionFactory : SignalR.ConnectionFactory
    {
        private readonly string _productInfo;

        public ConnectionFactory(string productInfo, string hubName, IServiceEndpointProvider provider, IServerNameProvider nameProvider, ILoggerFactory loggerFactory)
            : base(hubName, provider, nameProvider, loggerFactory)
        {
            _productInfo = productInfo;
        }

        public override Task<ConnectionContext> ConnectAsync(TransferFormat transferFormat, string connectionId, string target, CancellationToken cancellationToken = default, IDictionary<string, string> headers = null)
        {
            return base.ConnectAsync(transferFormat, connectionId, target, cancellationToken, new Dictionary<string, string> { { Constants.AsrsUserAgent, _productInfo } });
        }
    }
}
