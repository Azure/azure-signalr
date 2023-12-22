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
        private readonly string _connectionName;

        public string Name => Options.DefaultName;

        public ServiceOptionsSetup(IConfiguration configuration) : this(configuration, null)
        {
        }

        public ServiceOptionsSetup(IConfiguration configuration, string connectionName)
        {
            _configuration = configuration;
            _connectionName = connectionName;
        }

        public void Configure(ServiceOptions options)
        {
            var configuration = ParseConfiguration(_connectionName);

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

        private (string AppName, string ConnectionString, ServiceEndpoint[] Endpoints, ConfigurableServiceOptions configurableOptions) ParseConfiguration(string connectionName)
        {
            var sectionKey = string.IsNullOrEmpty(connectionName) ? Constants.Keys.AzureSignalRSectionKey : $"{Constants.Keys.AzureSignalRSectionKey}:{connectionName}";
            var options = _configuration.GetSection(sectionKey).Get<ConfigurableServiceOptions>();

            var appName = GetApplicationName(sectionKey);

            var connectionString = GetConnectionString(sectionKey, connectionName);

            var endpoints = GetEndpoints(sectionKey);

            return (appName, connectionString, endpoints, options);
        }

        private string GetApplicationName(string sectionKey)
        {
            // A known issue in previous version that the key ended with ":"
            return _configuration[$"{sectionKey}:ApplicationName:"] ?? _configuration[$"{sectionKey}:ApplicationName"];
        }

        private string GetConnectionString(string sectionKey, string connectionName)
        {
            // ConnectionStrings_connectionName takes the highest priority
            if (!string.IsNullOrEmpty(connectionName) && _configuration.GetConnectionString(connectionName) is string connectionString)
            {
                return connectionString;
            }

            var connectionStringKey = $"{sectionKey}:ConnectionString";
            // Fallback to ConnectionStrings:Azure:SignalR:ConnectionString format when the default one is not available
            return _configuration[connectionStringKey] ?? _configuration.GetConnectionString(connectionStringKey) ;
        }

        private ServiceEndpoint[] GetEndpoints(string sectionKey)
        {
            var endpointKey = $"{sectionKey}:ConnectionString";
            var endpoints = _configuration.GetEndpoints(endpointKey).ToArray();

            if (endpoints.Length == 0)
            {
                endpoints = _configuration.GetEndpoints($"ConnectionStrings:{endpointKey}").ToArray();
            }

            return endpoints;
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
