// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Azure.SignalR.Common.Endpoints;
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
            var appName = _configuration[Constants.Keys.ApplicationNameDefaultKeyPrefix];
            var stickyMode = ServerStickyMode.Disabled;
            var mode = _configuration[Constants.Keys.ServerStickyModeDefaultKey];
            if (!string.IsNullOrEmpty(mode))
            {
                Enum.TryParse(mode, true, out stickyMode);
            }

            var (connectionString, endpoints) = _configuration.GetSignalRServiceEndpoints(Constants.Keys.ConnectionStringDefaultKey);

            // Fallback to ConnectionStrings:Azure:SignalR:ConnectionString format when the default one is not available
            if (connectionString == null && endpoints.Length == 0)
            {
                (connectionString, endpoints) = _configuration.GetSignalRServiceEndpoints(Constants.Keys.ConnectionStringSecondaryKey);
            }

            return (appName, connectionString, stickyMode, endpoints);
        }
    }
}