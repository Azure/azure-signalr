// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceOptionsSetup : IConfigureOptions<ServiceOptions>
    {
        internal static readonly int DefaultConnectionCount = 5;
        private string _connectionString;

        public ServiceOptionsSetup(IConfiguration configuration)
        {
            _connectionString = configuration.GetSection(ServiceOptions.ConnectionStringDefaultKey).Value;
        }

        public void Configure(ServiceOptions options)
        {
            if (options.ConnectionCount == null)
            {
                options.ConnectionCount = DefaultConnectionCount;
            }
            if (options.ConnectionString == null)
            {
                options.ConnectionString = _connectionString;
            }
        }
    }
}
