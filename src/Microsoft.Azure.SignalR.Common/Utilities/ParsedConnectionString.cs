// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Azure.Core;

namespace Microsoft.Azure.SignalR;

#nullable enable

internal class ParsedConnectionString
{
    internal Uri Endpoint { get; }

    internal string? AccessKey { get; init; }

    internal TokenCredential TokenCredential { get; }

    internal Uri? ClientEndpoint { get; init; }

    internal Uri? ServerEndpoint { get; init; }

    public ParsedConnectionString(Uri endpoint, TokenCredential tokenCredential)
    {
        Endpoint = endpoint;
        TokenCredential = tokenCredential;
    }
}
