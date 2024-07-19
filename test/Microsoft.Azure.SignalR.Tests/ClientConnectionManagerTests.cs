using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests;

public class ClientConnectionManagerTests
{
    private async Task RemoveConnection(IClientConnectionManager manager, ClientConnectionContext ctx)
    {
        await Task.Delay(100);
        ctx.OnCompleted();
    }

    [Fact(Skip = "Disable high possibility failed cases until they are fixed")]
    public void TestAllClientConnectionsCompleted()
    {
        var manager = new ClientConnectionManager();

        var c1 = new ClientConnectionContext(new Protocol.OpenConnectionMessage("foo", new Claim[0]));
        var c2 = new ClientConnectionContext(new Protocol.OpenConnectionMessage("bar", new Claim[0]));

        manager.TryAddClientConnection(c1);
        manager.TryAddClientConnection(c2);

        _ = RemoveConnection(manager, c1);
        _ = RemoveConnection(manager, c2);

        var expected = manager.WhenAllCompleted();
        var actual = Task.WaitAny(
            expected,
            Task.Delay(TimeSpan.FromSeconds(1))
        );
        Assert.Equal(0, actual);
    }
}
