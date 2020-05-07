﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.SignalR.Management
{
    internal class RestApiEndpoint
    {
        public string Audience { get; }
        public string Token { get; }
        public IDictionary<string, StringValues> Query { get; set; }

        public RestApiEndpoint(string endpoint, string token)
        {
            Audience = endpoint;
            Token = token;
        }
    }
}
