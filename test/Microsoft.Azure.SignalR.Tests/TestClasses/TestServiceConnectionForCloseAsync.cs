// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;

namespace Microsoft.Azure.SignalR.Tests;

internal sealed class TestServiceConnectionForCloseAsync : TestServiceConnection
{
    public TestServiceConnectionForCloseAsync() : base(ServiceConnectionStatus.Connected, false)
    {
    }

    protected override Task OnClientConnectedAsync(OpenConnectionMessage openConnectionMessage)
    {
        return Task.CompletedTask;
    }
}
