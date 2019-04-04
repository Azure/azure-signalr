// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceOptionsSetup : IConfigureOptions<ServiceOptions>
    {
        private readonly string _connectionString;
        private readonly ServiceEndpoint[] _endpoints;
        private readonly bool _isEnabled;

        public ServiceOptionsSetup(IConfiguration configuration)
        {
            var (connectionString, endpoints, isEnabled) = GetEndpoint(configuration, Constants.ConnectionStringDefaultKey, Constants.ConnectionStringKeyPrefix);

            // Fallback to ConnectionStrings:Azure:SignalR:ConnectionString format when the default one is not available
            if (endpoints.Count == 0)
            {
                (connectionString, endpoints, isEnabled) = GetEndpoint(configuration, Constants.ConnectionStringSecondaryKey, Constants.ConnectionStringSecondaryKeyPrefix);
            }

            _connectionString = connectionString;
            _endpoints = endpoints.ToArray();
            _isEnabled = isEnabled;
        }

        public void Configure(ServiceOptions options)
        {
            // The default setup of ServiceOptions
            options.ConnectionString = _connectionString;
            options.Endpoints = _endpoints;
            options.IsEnabled = _isEnabled;
        }

        private static (string, List<ServiceEndpoint>, bool) GetEndpoint(IConfiguration configuration, string defaultKey, string keyPrefix)
        {
            var endpoints = new List<ServiceEndpoint>();
            string connectionString = null;
            bool isEnabled = true;
            foreach(var pair in configuration.AsEnumerable())
            {
                var key = pair.Key;
                if (key == defaultKey && !string.IsNullOrEmpty(pair.Value))
                {
                    connectionString = pair.Value;
                    endpoints.Add(new ServiceEndpoint(pair.Value));
                }

                if (key.StartsWith(keyPrefix) && !string.IsNullOrEmpty(pair.Value))
                {
                    endpoints.Add(new ServiceEndpoint(key, pair.Value));
                }

                if (key == Constants.AzureSignalREnabledKey && !string.IsNullOrEmpty(pair.Value))
                {
                    // Only apparently mark false will turn-off Azure SignalR Service
                    if (pair.Value.Equals("false", StringComparison.OrdinalIgnoreCase))
                    {
                        isEnabled = false;
                    }
                }
            }

            return (connectionString, endpoints, isEnabled);
        }
    }
}
