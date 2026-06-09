// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.Extensions.Primitives;

#nullable enable

namespace Microsoft.Azure.SignalR;

internal class RestApiEndpoint
{
    public string Audience { get; }

    public IDictionary<string, StringValues>? Query { get; set; }

    public RestApiEndpoint(string endpoint)
    {
        Audience = endpoint;
    }
}
