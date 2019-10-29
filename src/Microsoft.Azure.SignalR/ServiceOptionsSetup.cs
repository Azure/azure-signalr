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
        private readonly string _appName;
        private readonly ServerStickyMode _serverStickyMode;
        private readonly string _connectionString;
        private readonly ServiceEndpoint[] _endpoints;

        private readonly bool _gracefulShutdownEnabled = false;
        private readonly TimeSpan _shutdownTimeout = TimeSpan.FromSeconds(Constants.DefaultShutdownTimeoutInSeconds);

        public ServiceOptionsSetup(IConfiguration configuration)
        {
            _appName = configuration[Constants.ApplicationNameDefaultKeyPrefix];
            var mode = configuration[Constants.ServerStickyModeDefaultKey];
            if (!string.IsNullOrEmpty(mode))
            {
                Enum.TryParse(mode, true, out _serverStickyMode);
            }

            var (connectionString, endpoints) = GetEndpoint(configuration, Constants.ConnectionStringDefaultKey);

            // Fallback to ConnectionStrings:Azure:SignalR:ConnectionString format when the default one is not available
            if (connectionString == null && endpoints.Length == 0)
            {
                (connectionString, endpoints) = GetEndpoint(configuration, Constants.ConnectionStringSecondaryKey);
            }

            _connectionString = connectionString;
            _endpoints = endpoints;

            if (configuration[Constants.ServerGracefulShutdownKey] == "Enabled")
            {
                _gracefulShutdownEnabled = true;
            }

            if (Int32.TryParse(configuration[Constants.ServerGracefulShutdownTimeoutKey], out int timeout))
            {
                if (timeout < 1)
                {
                    _gracefulShutdownEnabled = false;
                }
                else
                {
                    timeout = timeout > 600 ? 600 : timeout;
                    _shutdownTimeout = TimeSpan.FromSeconds(timeout);
                }
            }
        }

        public void Configure(ServiceOptions options)
        {
            // The default setup of ServiceOptions
            options.ConnectionString = _connectionString;
            options.Endpoints = _endpoints;
            options.ApplicationName = _appName;
            options.ServerStickyMode = _serverStickyMode;
            options.EnableGracefulShutdown = _gracefulShutdownEnabled;
            options.ServerShutdownTimeout = _shutdownTimeout;
        }

        private static (string, ServiceEndpoint[]) GetEndpoint(IConfiguration configuration, string key)
        {
            var section = configuration.GetSection(key);
            var connectionString = section.Value;
            var endpoints = GetEndpoints(section.GetChildren()).ToArray();

            return (connectionString, endpoints);
        }

        private static IEnumerable<ServiceEndpoint> GetEndpoints(IEnumerable<IConfigurationSection> sections)
        {
            foreach (var section in sections)
            {
                foreach (var entry in section.AsEnumerable())
                {
                    if (!string.IsNullOrEmpty(entry.Value))
                    {
                        yield return new ServiceEndpoint(entry.Key, entry.Value);
                    }
                }
            }
        }
    }
}
