// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

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
        /// Gets or sets the total number of connections from SDK to Azure SignalR Service. Default value is 5.
        /// </summary>
        public int ConnectionCount { get; set; } = 5;

        /// <summary>
        /// Gets or sets the prefix to apply to each hub name
        /// </summary>
        public string ApplicationName { get; set; }

        /// <summary>
        /// Gets or sets the func to generate claims from <see cref="HttpContext" />.
        /// The claims will be included in the auto-generated token for clients.
        /// </summary>
        public Func<HttpContext, IEnumerable<Claim>> ClaimsProvider { get; set; } = null;

        /// <summary>
        /// Gets or sets the lifetime of auto-generated access token, which will be used to authenticate with Azure SignalR Service.
        /// Default value is one hour.
        /// </summary>
        public TimeSpan AccessTokenLifetime { get; set; } = Constants.Periods.DefaultAccessTokenLifetime;

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
        /// Specifies if server will shutdown gracefully. 
        /// Default value is false.
        /// </summary>
        internal bool EnableGracefulShutdown { get; set; } = false;

        /// <summary>
        /// Specifies the timeout of a graceful shutdown process (in seconds). 
        /// Default value is 30 seconds.
        /// </summary>
        internal TimeSpan ServerShutdownTimeout { get; set; } = Constants.Periods.DefaultShutdownTimeout;

        /// <summary>
        /// Specifies if the client-connection assigned to this server can be migrated to another server.
        /// Default value is 0.
        /// 1: Only migrate client-connection if server was shutdown gracefully.
        /// 2: Migrate client-connection even if server-connection was accidentally dropped. (Potential data losses)
        /// </summary>
        internal ServerConnectionMigrationLevel MigrationLevel { get; set; } = ServerConnectionMigrationLevel.Off;

        /// <summary>
        /// Gets or sets the proxy used when ServiceEndpoint will attempt to connect to Azure SignalR.
        /// </summary>
        public IWebProxy Proxy { get; set; }

        /// <summary>
        /// Gets or sets timeout waiting when scale multiple Azure SignalR Service endpoints.
        /// Default value is 5 minutes
        /// </summary>
        internal TimeSpan ServiceScaleTimeout { get; set; } = Constants.Periods.DefaultScaleTimeout;
    }
}
