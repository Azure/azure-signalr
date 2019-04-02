// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
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
        /// Gets or sets the total number of connections from SDK to Azure SignalR Service. Default value is 5.
        /// </summary>
        public int ConnectionCount { get; set; } = 5;

        /// <summary>
        /// Gets applicationName, which will be used as a prefix to apply to each hub name
        /// </summary>
        public string ApplicationName{ get; internal set; }

        /// <summary>
        /// Gets or sets whether the hub name wiull be prefixed with the ApplicationName
        /// </summary>
        public bool UseHubNamePrefix { get; set; }

        /// <summary>
        /// Gets or sets the func to generate claims from <see cref="IOwinContext" />.
        /// The claims will be included in the auto-generated token for clients.
        /// </summary>
        public Func<IOwinContext, IEnumerable<Claim>> ClaimsProvider { get; set; } = null;

        /// <summary>
        /// Gets or sets the lifetime of auto-generated access token, which will be used to authenticate with Azure SignalR Service.
        /// Default value is one hour.
        /// </summary>
        public TimeSpan AccessTokenLifetime { get; set; } = Constants.DefaultAccessTokenLifetime;

        public ServiceEndpoint[] Endpoints { get; set; }

        public ServiceOptions()
        {
            var count = ConfigurationManager.ConnectionStrings.Count;
            string connectionString = null;
            var endpoints = new List<ServiceEndpoint>();
            for (int i = 0; i < count; i++)
            {
                var setting = ConfigurationManager.ConnectionStrings[i];
                var (isDefault, endpoint) = GetEndpoint(setting.Name, this.ApplicationName, () => setting.ConnectionString);
                if (endpoint != null)
                {
                    if (isDefault)
                    {
                        connectionString = endpoint.ConnectionString;
                    }

                    endpoints.Add(endpoint);
                }
            }

            if (endpoints.Count == 0)
            {
                // Fallback to use AppSettings
                foreach(var key in ConfigurationManager.AppSettings.AllKeys)
                {
                    var (isDefault, endpoint) = GetEndpoint(key, this.ApplicationName, () => ConfigurationManager.AppSettings[key]);
                    if (endpoint != null)
                    {
                        if (isDefault)
                        {
                            connectionString = endpoint.ConnectionString;
                        }

                        endpoints.Add(endpoint);
                    }
                }
            }

            ConnectionString = connectionString;
            Endpoints = endpoints.ToArray();
        }

        private static (bool isDefault, ServiceEndpoint endpoint) GetEndpoint(string key, string appName, Func<string> valueGetter)
        {
            if (key == Constants.ConnectionStringDefaultKey && !string.IsNullOrEmpty(valueGetter()))
            {
                return (true, new ServiceEndpoint(valueGetter(), applicationName: appName));
            }

            if (key.StartsWith(Constants.ConnectionStringKeyPrefix) && !string.IsNullOrEmpty(valueGetter()))
            {
                return (false, new ServiceEndpoint(key, valueGetter(), appName));
            }

            return (false, null);
        }
    }
}
