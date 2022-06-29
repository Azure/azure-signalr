// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;

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

            var configurableOption = configuration.configurableOptions;
            if (configurableOption != null)
            {
                options.ServerStickyMode = GetConfiguredEnum(configurableOption.ServerStickyMode, options.ServerStickyMode);
                options.InitialHubServerConnectionCount = configurableOption.InitialHubServerConnectionCount ?? options.InitialHubServerConnectionCount;
                options.MaxHubServerConnectionCount = configurableOption.MaxHubServerConnectionCount ?? options.MaxHubServerConnectionCount;
                options.AccessTokenAlgorithm = GetConfiguredEnum(configurableOption.AccessTokenAlgorithm, options.AccessTokenAlgorithm);

                options.AccessTokenLifetime = GetConfiguredTimeSpanFromSeconds(configurableOption.AccessTokenLifetimeInSeconds, options.AccessTokenLifetime);
                options.ServiceScaleTimeout = GetConfiguredTimeSpanFromSeconds(configurableOption.ServiceScaleTimeoutInSeconds, options.ServiceScaleTimeout);
                options.MaxPollIntervalInSeconds = configurableOption.MaxPollIntervalInSeconds ?? options.MaxPollIntervalInSeconds;

                if (configurableOption.GracefulShutdown != null)
                {
                    var shutdownOptions = new GracefulShutdownOptions();
                    shutdownOptions.Mode = GetConfiguredEnum(configurableOption.GracefulShutdown.Mode, shutdownOptions.Mode);
                    shutdownOptions.Timeout = GetConfiguredTimeSpanFromSeconds(configurableOption.GracefulShutdown.TimeoutInSeconds, shutdownOptions.Timeout);
                    options.GracefulShutdown = shutdownOptions;
                }
            }
        }

        public IChangeToken GetChangeToken()
        {
            return _configuration.GetReloadToken();
        }

        private TimeSpan GetConfiguredTimeSpanFromSeconds(int? seconds, TimeSpan defaultValue)
        {
            return seconds == null ? defaultValue : TimeSpan.FromSeconds(seconds.Value);
        }

        private T GetConfiguredEnum<T>(string value, T defaultValue) where T : struct
        {
            if (Enum.TryParse<T>(value, true, out var result))
            {
                return result;
            }

            return defaultValue;
        }

        private (string AppName, string ConnectionString, ServiceEndpoint[] Endpoints, ConfigurableServiceOptions configurableOptions) ParseConfiguration()
        {
            var options = _configuration.GetSection(Constants.Keys.AzureSignalRSectionKey).Get<ConfigurableServiceOptions>();

            // For backward compatability, first read from prefix
            var appName = _configuration[Constants.Keys.ApplicationNameDefaultKeyPrefix] ?? _configuration[Constants.Keys.ApplicationNameDefaultKey];

            // Fallback to ConnectionStrings:Azure:SignalR:ConnectionString format when the default one is not available
            var connectionString = _configuration[Constants.Keys.ConnectionStringDefaultKey] ?? _configuration[Constants.Keys.ConnectionStringSecondaryKey];

            var endpoints = _configuration.GetEndpoints(Constants.Keys.ConnectionStringDefaultKey).ToArray();

            if (endpoints.Length == 0)
            {
                endpoints = _configuration.GetEndpoints(Constants.Keys.ConnectionStringSecondaryKey).ToArray();
            }

            return (appName, connectionString, endpoints, options);
        }

        private record class ConfigurableGracefulShutdownOptions(
            string Mode,
            int? TimeoutInSeconds
            )
        {
            public ConfigurableGracefulShutdownOptions() : this(null, null) { }
        }

        private record class ConfigurableServiceOptions(
            string ServerStickyMode,
            int? InitialHubServerConnectionCount,
            int? MaxHubServerConnectionCount,
            string AccessTokenAlgorithm,
            int? AccessTokenLifetimeInSeconds,
            int? ServiceScaleTimeoutInSeconds,
            int? MaxPollIntervalInSeconds,
            ConfigurableGracefulShutdownOptions GracefulShutdown
            )
        {
            public ConfigurableServiceOptions() : this(null, null, null, null, null, null, null, null) { }
        }
    }
}
