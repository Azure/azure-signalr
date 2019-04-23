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
        internal string ApplicationName{ get; set; }
        string IServiceEndpointOptions.ApplicationName => ApplicationName;

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
        /// Customize the multiple endpoints used
        /// </summary>
        public ServiceEndpoint[] Endpoints { get; set; }

        /// <summary>
        /// TODO: Enable this option when the runtime is ready
        /// Specifies the mode for server sticky, when client is always routed to the server which it first /negotiate with, we call it "server sticky mode".
        /// By default this mode is disabled
        /// </summary>
        public ServerStickyMode ServerStickyMode { get; set; }

        /// <summary>
        /// TODO: delete it when runtime is ready, don't expose it to the customer
        /// </summary>
        internal bool IsolateApplication { get; set; } = false;

        public ServiceOptions()
        {
            var count = ConfigurationManager.ConnectionStrings.Count;
            string connectionString = null;
            var endpoints = new List<ServiceEndpoint>();
            for (var i = 0; i < count; i++)
            {
                var setting = ConfigurationManager.ConnectionStrings[i];

                if (setting.Name == Constants.ConnectionStringDefaultKey)
                {
                    connectionString = setting.ConnectionString;
                }
                else if (setting.Name.StartsWith(Constants.ConnectionStringKeyPrefix) && !string.IsNullOrEmpty(setting.ConnectionString))
                {
                    endpoints.Add(new ServiceEndpoint(setting.Name, setting.ConnectionString));
                }
            }

            // Fallback to use AppSettings
            if (string.IsNullOrEmpty(connectionString) && endpoints.Count == 0)
            {
                foreach(var key in ConfigurationManager.AppSettings.AllKeys)
                {
                    if (key == Constants.ConnectionStringDefaultKey)
                    {
                        connectionString = ConfigurationManager.AppSettings[key];
                    }
                    else if (key.StartsWith(Constants.ConnectionStringKeyPrefix))
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
