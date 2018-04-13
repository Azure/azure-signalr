// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Azure.SignalR
{
    public class ServiceOptions
    {
        public static readonly int DefaultConnectionNumber = 5;

        // Connection string can be passed by setting this environment variable (default key)
        public static readonly string ConnectionStringDefaultKey = "AzureSignalRConnectionString";

        public ServiceOptions()
        {
            ConnectionNumber = DefaultConnectionNumber;
        }

        // the order to find connection string:
        // 1. ServiceOptions.ConnectionString
        // 2. Environment variable
        // 3. Exception for no connection string
        public string ConnectionString { get; set; }

        public int ConnectionNumber { get; set; }

        public Func<HttpContext, IEnumerable<Claim>> Claims { get; set; } = null;
    }
}
