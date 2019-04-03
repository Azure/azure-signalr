// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceOptionsSetup : IConfigureOptions<ServiceOptions>
    {
        private readonly string _appName;
        private readonly string _connectionString;
        private readonly ServiceEndpoint[] _endpoints;

        public ServiceOptionsSetup(IConfiguration configuration)
        {
            _appName = GetAppName(configuration);

            var (connectionString, endpoints) = GetEndpoint(configuration, Constants.ConnectionStringDefaultKey, Constants.ConnectionStringKeyPrefix, _appName);

            // Fallback to ConnectionStrings:Azure:SignalR:ConnectionString format when the default one is not available
            if (endpoints.Count == 0)
            {
                (connectionString, endpoints) = GetEndpoint(configuration, Constants.ConnectionStringSecondaryKey, Constants.ConnectionStringSecondaryKeyPrefix, _appName);
            }

            _connectionString = connectionString;
            _endpoints = endpoints.ToArray();
        }

        private string GetAppName(IConfiguration configuration)
        {
            foreach (var pair in configuration.AsEnumerable())
            {
                var key = pair.Key;
                if (key == Constants.ApplicationNameDefaultKey && !string.IsNullOrEmpty(pair.Value))
                {
                    return pair.Value;
                }

                if (key.StartsWith(Constants.ApplicationNameDefaultKeyPrefix) && !string.IsNullOrEmpty(pair.Value))
                {
                    return pair.Value;
                }
            }
            return string.Empty;
        }

        public void Configure(ServiceOptions options)
        {
            // The default setup of ServiceOptions
            options.ConnectionString = _connectionString;
            options.Endpoints = _endpoints;
            options.ApplicationName = _appName;
        }

        private static (string, List<ServiceEndpoint>) GetEndpoint(IConfiguration configuration, string defaultKey, string keyPrefix, string applicationName)
        {
            var endpoints = new List<ServiceEndpoint>();
            string connectionString = null;
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
            }

            return (connectionString, endpoints);
        }
    }
}
