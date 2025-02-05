// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Testing.xunit;
using Microsoft.Azure.SignalR.E2ETests.Common;
using Microsoft.Azure.SignalR.Tests.Common;

using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.E2ETests.ServerSDK;

[SkipIfConnectionStringNotPresent]
public sealed class CrossServerRerouteTests(ITestOutputHelper output) : VerifiableLoggedTest(output)
{
    private readonly TestHubConnectionFactory _hubConnectionFactory = new();

    private readonly ITestOutputHelper _output = output;

    [ConditionalFact]
    public async void TestMigrateClients()
    {
        using var server1 = new TestServer(_output);
        using var server2 = new TestServer(_output);
        using var server3 = new TestServer(_output);

        var host1 = await server1.StartAsync();
        var host2 = server2.StartAsync();

        var method = nameof(TestHub.Echo);

        var connections = _hubConnectionFactory.NewConnectionGroup(host1, 3);
        connections.Listen(method);
        await connections.StartAsync();

        await connections.SendAsync(method, "ping");
        await connections.ExpectInvokeAsync(method, "ping");

        #region connections should be migrated to server2
        await server2.StartAsync();
        await server1.StopAsync();

        connections.ResetInvoke(method);
        await connections.SendAsync(method, "pong");
        await connections.ExpectInvokeAsync(method, "pong");
        #endregion connections should be migrated to server2

        #region connections should be migrated to server3
        await server3.StartAsync();
        await server2.StopAsync();

        connections.ResetInvoke(method);
        await connections.SendAsync(method, "pang");
        await connections.ExpectInvokeAsync(method, "pang");
        #endregion connections should be migrated to server3

        await server3.StopAsync();
    }
}
