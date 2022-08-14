// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceOptionsSetup : IConfigureOptions<ServiceOptions>, IOptionsChangeTokenSource<ServiceOptions>
    {
        private readonly IConfiguration _configuration;

        public string Name => Options.DefaultName;

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
        }

        public IChangeToken GetChangeToken()
        {
            return _configuration.GetReloadToken();
        }

        private (string AppName, string ConnectionString, ServerStickyMode StickyMode, ServiceEndpoint[] Endpoints) ParseConfiguration()
        {
            // For backward compatability, first read from prefix
            var appName = _configuration[Constants.Keys.ApplicationNameDefaultKeyPrefix] ?? _configuration[Constants.Keys.ApplicationNameDefaultKey];
            var stickyMode = ServerStickyMode.Disabled;
            var mode = _configuration[Constants.Keys.ServerStickyModeDefaultKey];
            if (!string.IsNullOrEmpty(mode))
            {
                Enum.TryParse(mode, true, out stickyMode);
            }

            // Fallback to ConnectionStrings:Azure:SignalR:ConnectionString format when the default one is not available
            var connectionString = _configuration[Constants.Keys.ConnectionStringDefaultKey] ?? _configuration[Constants.Keys.ConnectionStringSecondaryKey];

            var endpoints = _configuration.GetEndpoints(Constants.Keys.ConnectionStringDefaultKey).ToArray();

            if (endpoints.Length == 0)
            {
                endpoints = _configuration.GetEndpoints(Constants.Keys.ConnectionStringSecondaryKey).ToArray();
            }

            return (appName, connectionString, stickyMode, endpoints);
        }
    }
}
