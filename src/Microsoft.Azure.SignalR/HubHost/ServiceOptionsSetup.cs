// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceOptionsSetup : IConfigureOptions<ServiceOptions>
    {
        internal static readonly int DefaultConnectionNumber = 5;

        public void Configure(ServiceOptions options)
        {
            if (options.ConnectionNumber == null)
            {
                options.ConnectionNumber = DefaultConnectionNumber;
            }
            if (options.ConnectionString == null)
            {
                options.ConnectionString = Environment.GetEnvironmentVariable(ServiceOptions.ConnectionStringDefaultKey);
            }
        }
    }
}
