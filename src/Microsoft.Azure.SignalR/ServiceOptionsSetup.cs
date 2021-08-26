// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceOptionsSetup : IConfigureOptions<ServiceOptions>, IOptionsChangeTokenSource<ServiceOptions>
    {
        private readonly IConfiguration _configuration;
        private readonly AzureComponentFactory _azureComponentFactory;

        public string Name => Options.DefaultName;

        public ServiceOptionsSetup(IConfiguration configuration, AzureComponentFactory azureComponentFactory)
        {
            _configuration = configuration;
            _azureComponentFactory = azureComponentFactory;
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

            // Fallback to ConnectionStrings:Azure:SignalR:ConnectionString format when the default one is not available
            var connectionString = _configuration[Constants.Keys.ConnectionStringDefaultKey] ?? _configuration[Constants.Keys.ConnectionStringSecondaryKey];

            var endpoints = _configuration.GetEndpoints(Constants.Keys.ConnectionStringDefaultKey).ToArray();

            if (endpoints.Length == 0)
            {
                endpoints = _configuration.GetEndpoints(Constants.Keys.ConnectionStringSecondaryKey).ToArray();
            }

            if(endpoints.Length == 0)
            {
                var section = _configuration.GetSection(Constants.Keys.AzureSignalRSectionKey);
                //get multiple named endpoints.
                var multipleEndpoints = _configuration.GetSection(Constants.Keys.AzureSignalREndpointsKey).GetEndpoints(_azureComponentFactory);
                //try to get the single identity-based nameless endpoint. 
                if (section.GetSection(Constants.Keys.IdentityBasedSingleEndpointKey).TryGetNamedEndpointFromIdentity(_azureComponentFactory, out var singleEndpoint))
                {
                    //reset the name
                    singleEndpoint.Name = string.Empty;
                    multipleEndpoints = multipleEndpoints.Append(singleEndpoint);
                }
                endpoints = multipleEndpoints.ToArray();
            }

            return (appName, connectionString, stickyMode, endpoints);
        }
    }
}