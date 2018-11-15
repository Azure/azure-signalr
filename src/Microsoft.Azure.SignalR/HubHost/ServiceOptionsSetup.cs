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
        private static readonly string ConnectionStringSecondaryKey =
            $"ConnectionStrings:{ServiceOptions.ConnectionStringDefaultKey}";

        private static readonly string ConnectionStringKeyPrefix = ServiceOptions.ConnectionStringDefaultKey + ":";

        private readonly string _connectionString;

        private readonly ConnectionEndpoint[] _connectionStrings;

        public ServiceOptionsSetup(IConfiguration configuration)
        {
            var connectionString = configuration.GetSection(ServiceOptions.ConnectionStringDefaultKey).Value;

            // Load connection string from "ConnectionStrings" section when default key doesn't exist or holds an empty value.
            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = configuration.GetSection(ConnectionStringSecondaryKey).Value;
            }

            _connectionString = connectionString;

            _connectionStrings = GetConnectionStrings(configuration).ToArray();
        }

        private IEnumerable<ConnectionEndpoint> GetConnectionStrings(IConfiguration configuration)
        {
            foreach (var section in configuration.GetChildren())
            {
                if (section.Key == ServiceOptions.ConnectionStringDefaultKey)
                {
                    yield return new ConnectionEndpoint(section.Key, section.Value, EndpointStatus.Active);
                }
                else if (section.Key.StartsWith(ConnectionStringKeyPrefix))
                {
                    // Azure:SignalR:ConnectionString:<name>:<status>
                    var status = section.Key.Substring(ConnectionStringKeyPrefix.Length);
                    var parts = status.Split(':');
                    if (parts.Length == 1)
                    {
                        yield return new ConnectionEndpoint(section.Key, section.Value, EndpointStatus.Active);
                    }
                    else
                    {
                        if (Enum.TryParse<EndpointStatus>(parts[1], out var endpointStatus))
                        {
                            yield return new ConnectionEndpoint(section.Key, section.Value, endpointStatus);
                        }
                        else
                        {
                            yield return new ConnectionEndpoint(section.Key, section.Value, EndpointStatus.Active);
                        }
                    }
                }
            }
        }

        public void Configure(ServiceOptions options)
        {
            if (options.ConnectionString == null)
            {
                options.ConnectionString = _connectionString;
            }

            if (options.ConnectionStrings == null)
            {
                options.ConnectionStrings = _connectionStrings;
            }
        }
    }

    public class ConnectionEndpoint
    {
        public string Key { get; }

        public string Endpoint { get; }

        public EndpointStatus Status { get; }

        public ConnectionEndpoint(string key, string endpoint, EndpointStatus status, TimeSpan lifeSpan)
        {
            Key = key;
            Endpoint = endpoint;
            Status = status;
            Provider = new ServiceEndpointProvider(endpoint, lifeSpan);
        }
    }

    public enum EndpointStatus
    {
        Active,
        Disabled,
        New,
    }
}
