// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;

namespace Microsoft.Azure.SignalR.Tests.Common;

internal class TestMicrosoftEntraAccessKey : MicrosoftEntraAccessKey
{
    public string Token { get; } = Guid.NewGuid().ToString();

    public TestMicrosoftEntraAccessKey() : base(new Uri("http://localhost:80"), new DefaultAzureCredential())
    {
    }

    public override Task<string> GetMicrosoftEntraTokenAsync(CancellationToken ctoken = default)
    {
        return Task.FromResult(Token);
    }
}
