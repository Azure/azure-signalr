// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.AspNetCore.SignalR.Internal.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Azure.SignalR
{
    public class CloudSignalR
    {
        public class ConnectionString
        {
            public string Endpoint { get; set; }

            public string AccessKey { get; set; }
        }

        private const string EndpointProperty = "endpoint";
        private const string AccessKeyProperty = "accesskey";

        public static ConnectionString ParseConnectionString(string connectionString)
        {
            if (!string.IsNullOrEmpty(connectionString))
            {
                var dict = connectionString.Split(new[] {';'}, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Split(new[] {'='}, 2))
                    .ToDictionary(t => t[0].Trim().ToLower(), t => t[1].Trim(),
                        StringComparer.OrdinalIgnoreCase);
                if (dict.ContainsKey(EndpointProperty) && dict.ContainsKey(AccessKeyProperty))
                {
                    return new ConnectionString
                    {
                        Endpoint = dict[EndpointProperty],
                        AccessKey = dict[AccessKeyProperty]
                    };
                }
            }

            throw new ArgumentException($"Invalid Azure SignalR connection string: {connectionString}");
        }

        public static EndpointProvider CreateEndpointProviderFromConnectionString(string connectionString)
        {
            var connStr = ParseConnectionString(connectionString);
            return new EndpointProvider(connStr.Endpoint);
        }

        public static TokenProvider CreateTokenProviderFromConnectionString(string connectionString)
        {
            var connStr = ParseConnectionString(connectionString);
            return new TokenProvider(connStr.Endpoint, connStr.AccessKey);
        }

        public static HubProxy CreateHubProxyFromConnectionString<THub>(string connectionString) where THub : Hub
        {
            return CreateHubProxyFromConnectionString<THub>(connectionString, null);
        }

        public static HubProxy CreateHubProxyFromConnectionString<THub>(string connectionString, HubProxyOptions options)
            where THub : Hub
        {
            return CreateHubProxyFromConnectionString(connectionString, typeof(THub).Name, options);
        }

        public static HubProxy CreateHubProxyFromConnectionString(string connectionString, string hubName)
        {
            return CreateHubProxyFromConnectionString(connectionString, hubName, null);
        }

        public static HubProxy CreateHubProxyFromConnectionString(string connectionString, string hubName, HubProxyOptions options)
        {
            var connStr = ParseConnectionString(connectionString);
            return new HubProxy(connStr.Endpoint, connStr.AccessKey, hubName);
        }

        public static void ConfigureAuthorization(Action<AuthorizationOptions> configure)
        {
            if (configure != null) _authorizationConfigure = configure;
        }

        private static Action<AuthorizationOptions> _authorizationConfigure = _ => { };

        private static IServiceProvider _externalServiceProvider = null;

        private static readonly Lazy<IServiceProvider> _internalServiceProvider =
            new Lazy<IServiceProvider>(
                () =>
                {
                    var serviceCollection = new ServiceCollection();
                    var signalRServerBuilder = serviceCollection.AddSignalR()
                                                                .AddAzureSignalR();
                    signalRServerBuilder.Services.AddLogging();
                    signalRServerBuilder.Services.AddAuthorization(_authorizationConfigure);
                    return signalRServerBuilder.Services.BuildServiceProvider();
                });

        public static IServiceProvider ServiceProvider
        {
            get => _externalServiceProvider ?? _internalServiceProvider.Value;
            internal set => _externalServiceProvider = value;
        }
    }
}
