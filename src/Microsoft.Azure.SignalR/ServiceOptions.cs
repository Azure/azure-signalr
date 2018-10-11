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
    public class ServiceOptions
    {
        /// <summary>
        /// The key which will be used to read connection string from environment variables.
        /// </summary>
        public static readonly string ConnectionStringDefaultKey = "Azure:SignalR:ConnectionString";

        // Default access token lifetime
        internal static readonly TimeSpan DefaultAccessTokenLifetime = TimeSpan.FromHours(1);

        /// <summary>
        /// Gets or sets the connection string of Azure SignalR Service instance.
        /// </summary>
        public string ConnectionString { get; set; } = null;

        /// <summary>
        /// Gets or sets the total number of connections from SDK to Azure SignalR Service. Default value is 5.
        /// </summary>
        public int ConnectionCount { get; set; } = 1;

        /// <summary>
        /// Gets or sets the func to generate claims from <see cref="HttpContext" />.
        /// The claims will be included in the auto-generated token for clients.
        /// </summary>
        public Func<HttpContext, IEnumerable<Claim>> ClaimsProvider { get; set; } = null;

        /// <summary>
        /// Gets or sets the lifetime of auto-generated access token, which will be used to authenticate with Azure SignalR Service.
        /// Default value is one hour.
        /// </summary>
        public TimeSpan AccessTokenLifetime { get; set; } = DefaultAccessTokenLifetime;
    }
}
