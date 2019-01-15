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
        /// Gets or sets the func to generate claims from <see cref="IOwinContext" />.
        /// The claims will be included in the auto-generated token for clients.
        /// </summary>
        public Func<IOwinContext, IEnumerable<Claim>> ClaimsProvider { get; set; } = null;

        /// <summary>
        /// Gets or sets the lifetime of auto-generated access token, which will be used to authenticate with Azure SignalR Service.
        /// Default value is one hour.
        /// </summary>
        public TimeSpan AccessTokenLifetime { get; set; } = Constants.DefaultAccessTokenLifetime;

        /// <summary>
        /// TODO: expose to customer
        /// Gets or sets list of endpoints
        /// </summary>
        internal ServiceEndpoint[] Endpoints { get; set; }

        ServiceEndpoint[] IServiceEndpointOptions.Endpoints => Endpoints;

        public ServiceOptions()
        {
            var count = ConfigurationManager.ConnectionStrings.Count;
            string connectionString = null;
            var endpoints = new List<ServiceEndpoint>();
            for (int i = 0; i < count; i++)
            {
                var setting = ConfigurationManager.ConnectionStrings[i];
                var se = GetEndpoint(setting.Name, k => setting.ConnectionString);
                if (se.Item2 != null)
                {
                    if (se.Item1)
                    {
                        connectionString = se.Item2.ConnectionString;
                    }

                    endpoints.Add(se.Item2);
                }
            }

            if (endpoints.Count == 0)
            {
                // Fallback to use AppSettings
                foreach(var key in ConfigurationManager.AppSettings.AllKeys)
                {
                    var se = GetEndpoint(key, k => ConfigurationManager.AppSettings[k]);
                    if (se.Item2 != null)
                    {
                        if (se.Item1)
                        {
                            connectionString = se.Item2.ConnectionString;
                        }

                        endpoints.Add(se.Item2);
                    }
                }
            }

            // Load connection string from "Azure:SignalR:ConnectionString" section or key starts with "Azure:SignalR:ConnectionString:" when default key doesn't exist or holds an empty value.
            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = endpoints.FirstOrDefault()?.ConnectionString;
            }

            ConnectionString = connectionString;
            Endpoints = endpoints.ToArray();
        }

        private static (bool, ServiceEndpoint) GetEndpoint(string key, Func<string, string> valueGetter)
        {
            if (key == Constants.ConnectionStringDefaultKey && !string.IsNullOrEmpty(valueGetter(key)))
            {
                return (true, new ServiceEndpoint(valueGetter(key)));
            }

            if (key.StartsWith(Constants.ConnectionStringKeyPrefix) && !string.IsNullOrEmpty(valueGetter(key)))
            {
                return (false, new ServiceEndpoint(key, valueGetter(key)));
            }

            return (false, null);
        }
    }
}
