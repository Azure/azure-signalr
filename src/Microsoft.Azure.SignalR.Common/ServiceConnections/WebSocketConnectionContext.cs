// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Azure.SignalR.Connections.Client.Internal;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    /// <summary>
    /// TODO: Implement Features
    /// </summary>
    internal class WebSocketConnectionContext : ConnectionContext
    {
        private readonly WebSocketsTransport _websocketTransport;

        /// <summary>
        /// TODO: get from service handshake
        /// </summary>
        public override string ConnectionId { get; set; }

        public override IFeatureCollection Features { get; } = new FeatureCollection();

        public override IDictionary<object, object> Items { get; set; } = new ConnectionItems();

        public override IDuplexPipe Transport { get; set; }

        public WebSocketConnectionContext(WebSocketConnectionOptions httpConnectionOptions, ILoggerFactory loggerFactory, Func<Task<string>> accessTokenProvider)
        {
            Transport = _websocketTransport = new WebSocketsTransport(httpConnectionOptions, loggerFactory, accessTokenProvider);
            ConnectionId = "sc_" + Guid.NewGuid();
        }

        public async Task StartAsync(Uri url, CancellationToken cancellationToken = default)
        {
            await _websocketTransport.StartAsync(url, cancellationToken).ForceAsync();
        }

        public Task StopAsync()
        {
            return _websocketTransport.StopAsync();
        }
    }
}
