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
        private readonly IConfiguration _configuration;

        private readonly bool _gracefulShutdownEnabled = false;
        private readonly TimeSpan _shutdownTimeout = TimeSpan.FromSeconds(Constants.DefaultShutdownTimeoutInSeconds);

        public ServiceOptionsSetup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void Configure(ServiceOptions options)
        {
            var configuration = ParseConfiguration();

            options.ConnectionString = configuration.ConnectionString;
            options.Endpoints = configuration.Endpoints;
            options.ApplicationName = configuration.AppName;
            options.ServerStickyMode = configuration.StickyMode;

            options.EnableGracefulShutdown = _gracefulShutdownEnabled;
            options.ServerShutdownTimeout = _shutdownTimeout;
        }

        private (string AppName, string ConnectionString, ServerStickyMode StickyMode, ServiceEndpoint[] Endpoints) ParseConfiguration()
        {
            var appName = _configuration[Constants.ApplicationNameDefaultKeyPrefix];
            var stickyMode = ServerStickyMode.Disabled;
            var mode = _configuration[Constants.ServerStickyModeDefaultKey];
            if (!string.IsNullOrEmpty(mode))
            {
                Enum.TryParse(mode, true, out stickyMode);
            }

            var (connectionString, endpoints) = GetEndpoint(_configuration, Constants.ConnectionStringDefaultKey);

            // Fallback to ConnectionStrings:Azure:SignalR:ConnectionString format when the default one is not available
            if (connectionString == null && endpoints.Length == 0)
            {
                (connectionString, endpoints) = GetEndpoint(_configuration, Constants.ConnectionStringSecondaryKey);
            }

            // Get endpoints
            var endpoints1 = _configuration.GetSection(Constants.EndpointsDefaultKey)?
                .GetChildren().Select(
                c => new ServiceEndpoint
                (
                    c.GetSection("ConnectionString").Value,
                    (EndpointType)Enum.Parse(typeof(EndpointType), c.GetSection("EndpointType").Value),
                    c.GetSection("Name").Value
                )).ToArray();

            var totalEndpoints = endpoints1.Length > 0 ? endpoints.Union(endpoints1).Distinct().ToArray() : endpoints;
            return (appName, connectionString, stickyMode, totalEndpoints);
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
