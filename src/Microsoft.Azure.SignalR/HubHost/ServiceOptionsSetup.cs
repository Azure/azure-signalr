// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceOptionsSetup : IConfigureOptions<ServiceOptions>
    {
        private static readonly string ConnectionStringSecondaryKey =
            $"ConnectionStrings:{ServiceOptions.ConnectionStringDefaultKey}";

        private readonly string _connectionString;

        public ServiceOptionsSetup(IConfiguration configuration)
        {
            var connectionString = configuration.GetSection(ServiceOptions.ConnectionStringDefaultKey).Value;

            // Load connection string from "ConnectionStrings" section when default key doesn't exist or holds an empty value.
            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = configuration.GetSection(ConnectionStringSecondaryKey).Value;
            }

            _connectionString = connectionString;
        }

        public void Configure(ServiceOptions options)
        {
            if (options.ConnectionString == null)
            {
                options.ConnectionString = _connectionString;
            }
        }
    }
}
