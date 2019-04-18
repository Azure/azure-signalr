// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
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
        public TimeSpan AccessTokenLifetime { get; set; } = Constants.DefaultAccessTokenLifetime;

        /// <summary>
        /// Gets or sets list of endpoints
        /// </summary>
        public ServiceEndpoint[] Endpoints { get; set; }

        /// <summary>
        /// Specifies the mode for server sticky, when client is always routed to the server which it first /negotiate with, we call it "server sticky mode".
        /// NOTE: This feature is not available until May 13th when Azure SignalR Service is updated
        /// </summary>
        public ServerStickyMode ServerStickyMode { get; set; } = ServerStickyMode.Disabled;
    }
}
