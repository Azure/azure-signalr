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
