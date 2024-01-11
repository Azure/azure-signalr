// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;

namespace Microsoft.Azure.SignalR
{
    /// <summary>
    /// Configurable options when using Azure SignalR Service.
    /// </summary>
    public class ServiceOptions : IServiceEndpointOptions
    {
        /// <summary>
        /// Gets or sets the connection string of Azure SignalR Service instance.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the initial number of connections per hub from SDK to Azure SignalR Service. Default value is 5. 
        /// Usually keep it as the default value is enough. During runtime, the SDK might start new server connections for performance tuning or load balancing. 
        /// When you have big number of clients, you can give it a larger number for better throughput.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Please use InitialHubServerConnectionCount instead.")]
        public int ConnectionCount
        {
            get => InitialHubServerConnectionCount;
            set => InitialHubServerConnectionCount = value;
        }

        /// <summary>
        /// Gets or sets the initial number of connections per hub from SDK to Azure SignalR Service.
        /// Default value is 5. 
        /// Usually keep it as the default value is enough. When you have big number of clients, you can give it a larger number for better throughput.
        /// During runtime, the SDK might start new server connections for performance tuning or load balancing. 
        /// </summary>
        public int InitialHubServerConnectionCount { get; set; } = 5;

        /// <summary>
        /// Specifies the max server connection count allowed per hub from SDK to Azure SignalR Service. 
        /// During runtime, the SDK might start new server connections for performance tuning or load balancing.
        /// By default a new server connection starts whenever needed.
        /// When the max allowed server connection count is configured, the SDK does not start new connections when server connection count reaches the limit.
        /// </summary>
        public int? MaxHubServerConnectionCount { get; set; }

        /// <summary>
        /// Gets applicationName, which will be used as a prefix to apply to each hub name. 
        /// Should be prefixed with alphabetic characters and only contain alpha-numeric characters or underscore.
        /// </summary>
        public string ApplicationName { get; set; }

        /// <summary>
        /// Gets or sets the func to generate claims from <see cref="HttpContext" />.
        /// The claims will be included in the auto-generated token for clients.
        /// </summary>
        public Func<HttpContext, IEnumerable<Claim>> ClaimsProvider { get; set; } = null;

        /// <summary>
        /// Gets or sets the func to set diagnostic client filter from <see cref="HttpContext" />.
        /// The clients will be regarded as diagnostic client only if the function returns true.
        /// </summary>
        public Func<HttpContext, bool> DiagnosticClientFilter { get; set; } = null;

        /// <summary>
        /// Gets or sets the lifetime of auto-generated access token, which will be used to authenticate with Azure SignalR Service.
        /// Default value is one hour.
        /// </summary>
        public TimeSpan AccessTokenLifetime { get; set; } = Constants.Periods.DefaultAccessTokenLifetime;

        /// <summary>
        /// Gets or sets the access token generate algorithm, supports HmacSha256 or HmacSha512
        /// Default value is HmacSha256
        /// </summary>
        public AccessTokenAlgorithm AccessTokenAlgorithm { get; set; } = AccessTokenAlgorithm.HS256;

        /// <summary>
        /// Gets or sets list of endpoints
        /// </summary>
        public ServiceEndpoint[] Endpoints { get; set; }

        /// <summary>
        /// Specifies the mode for server sticky, when client is always routed to the server which it first /negotiate with, we call it "server sticky mode".
        /// By default it is disabled
        /// </summary>
        public ServerStickyMode ServerStickyMode { get; set; } = ServerStickyMode.Disabled;

        /// <summary>
        /// Specifies if the client-connection assigned to this server can be migrated to another server.
        /// Default value is 0.
        /// 1: Only migrate client-connection if server was shutdown gracefully.
        /// 2: Migrate client-connection even if server-connection was accidentally dropped. (Potential data losses)
        /// </summary>
        public GracefulShutdownOptions GracefulShutdown { get; set; } = new GracefulShutdownOptions();

        /// <summary>
        /// Gets or sets the proxy used when ServiceEndpoint will attempt to connect to Azure SignalR.
        /// </summary>
        public IWebProxy Proxy { get; set; }

        /// <summary>
        /// Gets or sets timeout waiting when scale multiple Azure SignalR Service endpoints.
        /// Default value is 5 minutes
        /// </summary>
        public TimeSpan ServiceScaleTimeout { get; set; } = Constants.Periods.DefaultScaleTimeout;

        /// <summary>
        /// Gets or sets the interval in seconds used by the Azure SignalR Service to timeout idle LongPolling connections.
        /// Default value is 5, limited to [1, 300].
        /// </summary>
        public int? MaxPollIntervalInSeconds { get; set; }

        /// <summary>
        /// Gets or sets a function which accepts <see cref="HttpContext"/> and returns a bitmask combining one or more <see cref="HttpTransportType"/> values that specify what transports the service should use to receive HTTP requests.
        /// </summary>
        public Func<HttpContext, HttpTransportType> TransportTypeDetector { get; set; } = null;
    }
}
