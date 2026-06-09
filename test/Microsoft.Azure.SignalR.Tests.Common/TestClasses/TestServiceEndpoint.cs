// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Azure.Core;

namespace Microsoft.Azure.SignalR.Tests.Common;

#nullable enable

internal class TestServiceEndpoint : ServiceEndpoint
{
    private static readonly Uri DefaultEndpoint = new("https://localhost");

    private const string DefaultConnectionString = "Endpoint=https://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ;Version=1.0";

    public TestServiceEndpoint(string name = "", string? connectionString = null) : base(connectionString ?? DefaultConnectionString, name: name)
    {
    }

    public TestServiceEndpoint(TokenCredential tokenCredential) : base(DefaultEndpoint, tokenCredential)
    {
    }
}