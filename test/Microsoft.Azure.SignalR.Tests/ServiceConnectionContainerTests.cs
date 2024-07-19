using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests;

public class ServiceConnectionContainerTests
{

    private async Task MockServiceAsync(TestServiceConnectionForCloseAsync conn)
    {
        IServiceProtocol proto = new ServiceProtocol();

        await conn.ConnectionCreated;

        // open 2 new connections (to create 2 new outgoing tasks
        proto.WriteMessage(new OpenConnectionMessage(Guid.NewGuid().ToString(), new Claim[0]), conn.Application.Output);
        proto.WriteMessage(new OpenConnectionMessage(Guid.NewGuid().ToString(), new Claim[0]), conn.Application.Output);
        await conn.Application.Output.FlushAsync();

        while (true)
        {
            var result = await conn.Application.Input.ReadAsync();
            var buffer = result.Buffer;

            try
            {
                // write back a FinAck after receiving a Fin
                if (proto.TryParseMessage(ref buffer, out ServiceMessage message))
                {
                    if (RuntimeServicePingMessage.IsFin(message))
                    {
                        var pong = RuntimeServicePingMessage.GetFinAckPingMessage();
                        proto.WriteMessage(pong, conn.Application.Output);
                        await conn.Application.Output.FlushAsync();
                        break;
                    }
                }
            }
            finally
            {
                conn.Application.Input.AdvanceTo(buffer.Start, buffer.End);
            }
        }
    }

    private PingMessage BuildPingMessage(string key, string val)
    {
        return new PingMessage
        {
            Messages = new string[2] { key, val }
        };
    }

    private async Task MockServiceAsyncWithException(TestServiceConnectionForCloseAsync conn)
    {
        IServiceProtocol proto = new ServiceProtocol();

        await conn.ConnectionCreated;

        // open 2 new connections (to create 2 new outgoing tasks
        proto.WriteMessage(new OpenConnectionMessage(Guid.NewGuid().ToString(), new Claim[0]), conn.Application.Output);
        proto.WriteMessage(new OpenConnectionMessage(Guid.NewGuid().ToString(), new Claim[0]), conn.Application.Output);
        await conn.Application.Output.FlushAsync();

        await Task.Delay(TimeSpan.FromSeconds(1));
        proto.WriteMessage(BuildPingMessage("_exception", "1"), conn.Application.Output);
        await conn.Application.Output.FlushAsync();
    }

    private async Task AssertTask(Task task, TimeSpan timeout)
    {
        // prevent our test cases from running permanently
        Task r = await Task.WhenAny(task, Task.Delay(timeout));
        Assert.Equal(r, task);
    }

    [Fact]
    public async void TestCloseAsync()
    {
        var conn = new TestServiceConnectionForCloseAsync();
        var hub = new TestHubServiceEndpoint();
        using var container = new TestBaseServiceConnectionContainer(new List<IServiceConnection> { conn }, hub);

        _ = conn.StartAsync();
        _ = MockServiceAsync(conn);

        // close connection after 1 seconds.
        await Task.Delay(TimeSpan.FromSeconds(1));
        // await AssertTask(container.CloseClientConnectionForTest(conn), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void TestCloseAsyncWithoutStartAsync()
    {
        var conn = new TestServiceConnectionForCloseAsync();
        var hub = new TestHubServiceEndpoint();
        using var container = new TestBaseServiceConnectionContainer(new List<IServiceConnection> { conn }, hub);

        // await AssertTask(container.CloseClientConnectionForTest(conn), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async void TestCloseAsyncWithExceptionAndNoFinAck()
    {
        var conn = new TestServiceConnectionForCloseAsync();
        var hub = new TestHubServiceEndpoint();
        using var container = new TestBaseServiceConnectionContainer(new List<IServiceConnection> { conn }, hub);

        _ = conn.StartAsync();
        _ = MockServiceAsyncWithException(conn);

        // close connection after 2 seconds to make sure we have received an exception.
        await Task.Delay(TimeSpan.FromSeconds(2));
        // TODO double check if we received an exception.
        // await AssertTask(container.CloseClientConnectionForTest(conn), TimeSpan.FromSeconds(5));
    }
}
