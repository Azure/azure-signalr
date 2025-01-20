// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Azure.SignalR.Tests;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.E2ETests.SignalR;

public class SameServerRerouteTests(ITestOutputHelper output) : VerifiableLoggedTest(output)
{
    private const string ConnectionString = "Endpoint=http://localhost:8080;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGH;Version=1.0;";

    private readonly ITestOutputHelper _output = output;

    [Fact]
    public async Task Test()
    {
        var hub = nameof(TestHub);

        var server = new TestServer(_output);
        var serverUrl = await server.StartAsync(new Dictionary<string, string>()
        {
            { TestConstants.ConnectionString, ConnectionString },
        });

        var manager = server.ServiceProvider.GetRequiredService<IServiceConnectionManager<TestHub>>();
        var c = Assert.IsType<MultiEndpointServiceConnectionContainer>(manager.ServiceConnectionContainer);
        var container = Assert.IsType<StrongServiceConnectionContainer>(c.FirstServiceConnectionContainer());
        Assert.Equal(2, container.ServiceConnections.Count);

        var hubConnection = new HubConnectionBuilder().WithUrl($"{serverUrl}/{hub}?user=foo").Build();
        await hubConnection.StartAsync();
        Assert.NotEmpty(hubConnection.ConnectionId);

        var clientManager = server.ServiceProvider.GetRequiredService<IClientConnectionManager>();
        Assert.True(clientManager.TryGetClientConnection(hubConnection.ConnectionId, out var clientConn));

        var source = new CancellationTokenSource(TimeSpan.FromHours(1));
        var task = HandleHubConnectionAsync(hubConnection, clientConn, source.Token);

        await task;
    }

    private async Task HandleHubConnectionAsync(HubConnection connection, IClientConnection clientConn, CancellationToken ctoken)
    {
        var method = nameof(TestHub.Echo);
        int send = 0, recv = 0;
        var prev = clientConn.ServiceConnection.ConnectionId;

        void action(string message)
        {
            if (clientConn.ServiceConnection.ConnectionId != prev)
            {
                prev = clientConn.ServiceConnection.ConnectionId;
            }
            Interlocked.Increment(ref recv);
        }

        connection.On(method, (Action<string>)action);

        while (!ctoken.IsCancellationRequested)
        {
            try
            {
                Interlocked.Increment(ref send);
                await connection.SendAsync(method, "ping", ctoken);
                await Task.Delay(1000, ctoken);
            }
            catch (OperationCanceledException)
            {
            }
        }

        Assert.True(send - recv <= 1);
    }
}
