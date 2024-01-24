// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Net;
using System.Security.Claims;
using Microsoft.Owin;

namespace Microsoft.Azure.SignalR.AspNet
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
        /// Gets applicationName, which will be used as a prefix to apply to each hub name
        /// </summary>
        internal string ApplicationName { get; set; }
        string IServiceEndpointOptions.ApplicationName => ApplicationName;

        /// <summary>
        /// Gets or sets the func to generate claims from <see cref="IOwinContext" />.
        /// The claims will be included in the auto-generated token for clients.
        /// </summary>
        public Func<IOwinContext, IEnumerable<Claim>> ClaimsProvider { get; set; } = null;

        /// <summary>
        /// Gets or sets the func to set diagnostic client filter from <see cref="IOwinContext" />.
        /// The clients will be regarded as diagnostic client only if the function returns true.
        /// </summary>
        public Func<IOwinContext, bool> DiagnosticClientFilter { get; set; } = null;

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
        /// Customize the multiple endpoints used
        /// </summary>
        public ServiceEndpoint[] Endpoints { get; set; }

        /// <summary>
        /// Specifies the mode for server sticky, when client is always routed to the server which it first /negotiate with, we call it "server sticky mode".
        /// By default this mode is disabled
        /// </summary>
        public ServerStickyMode ServerStickyMode { get; set; }

        /// <summary>
        /// Gets or sets the proxy used when ServiceEndpoint will attempt to connect to Azure SignalR.
        /// </summary>
        public IWebProxy Proxy { get; set; }

        /// <summary>
        /// Gets or sets the interval in seconds used by the Azure SignalR Service to timeout idle LongPolling connections
        /// Default value is 5, limited to [1, 300].
        /// </summary>
        public int? MaxPollIntervalInSeconds { get; set; }

        public ServiceOptions()
        {
            var count = ConfigurationManager.ConnectionStrings.Count;
            string connectionString = null;
            var endpoints = new List<ServiceEndpoint>();
            var connectionStringKeyPrefix = $"{Constants.Keys.ConnectionStringDefaultKey}:";
            for (var i = 0; i < count; i++)
            {
                var setting = ConfigurationManager.ConnectionStrings[i];

                if (setting.Name == Constants.Keys.ConnectionStringDefaultKey)
                {
                    connectionString = setting.ConnectionString;
                }
                else if (setting.Name.StartsWith(connectionStringKeyPrefix) && !string.IsNullOrEmpty(setting.ConnectionString))
                {
                    endpoints.Add(new ServiceEndpoint(setting.Name, setting.ConnectionString));
                }
            }

            // Fallback to use AppSettings
            if (string.IsNullOrEmpty(connectionString) && endpoints.Count == 0)
            {
                foreach (var key in ConfigurationManager.AppSettings.AllKeys)
                {
                    if (key == Constants.Keys.ConnectionStringDefaultKey)
                    {
                        connectionString = ConfigurationManager.AppSettings[key];
                    }
                    else if (key.StartsWith(connectionStringKeyPrefix))
                    {
                        var value = ConfigurationManager.AppSettings[key];
                        if (!string.IsNullOrEmpty(value))
                        {
                            endpoints.Add(new ServiceEndpoint(key, value));
                        }
                    }
                }
            }

            ConnectionString = connectionString;
            Endpoints = endpoints.ToArray();
        }
    }
}
