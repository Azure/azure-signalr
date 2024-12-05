// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR;

#nullable enable

internal class ParsedConnectionString
{
    internal Uri Endpoint { get; }

    internal IAccessKey? AccessKey { get; set; }

    internal Uri? ClientEndpoint { get; set; }

    internal Uri? ServerEndpoint { get; set; }

    public ParsedConnectionString(Uri endpoint)
    {
        Endpoint = endpoint;
    }
}
