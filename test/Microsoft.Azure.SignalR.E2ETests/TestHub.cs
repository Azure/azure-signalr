// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.E2ETests;

internal sealed class TestHub : Hub
{
    public void Echo(string message)
    {
        Clients.Caller.SendAsync(nameof(Echo), message);
    }
}
