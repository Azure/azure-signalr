// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Tests;

internal sealed class TestHubContext<THub> : IHubContext<THub> where THub : Hub
{
    public IHubClients Clients => throw new NotImplementedException();

    public IGroupManager Groups => throw new NotImplementedException();

    public static TestHubContext<THub> GetInstance()
    {
        return new TestHubContext<THub>();
    }
}
