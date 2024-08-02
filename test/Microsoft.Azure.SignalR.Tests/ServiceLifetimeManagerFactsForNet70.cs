// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests;

#if NET7_0_OR_GREATER

public class ServiceLifetimeManagerFactsForNet70 : ServiceLifetimeManagerFacts
{
    private static readonly List<string> TestConnectionIds = new List<string> { "connection1", "connection2" };

    [Theory]
    [InlineData("json", true)]
    [InlineData("json", false)]
    [InlineData("messagepack", true)]
    [InlineData("messagepack", false)]
    public async void TestClientInvocationOneService(string protocol, bool isCompletionWithResult)
    {
        var serviceConnection = new TestServiceConnection();
        var serviceConnectionManager = new TestServiceConnectionManager<TestHub>();

        var clientInvocationManager = new DefaultClientInvocationManager();
        var clientConnectionContext = GetClientConnectionContextWithConnection(TestConnectionIds[1], protocol);

        var serviceLifetimeManager = GetTestClientInvocationServiceLifetimeManager(serviceConnection, serviceConnectionManager, new ClientConnectionManager(), clientInvocationManager, clientConnectionContext);

        var invocationResult = "invocation-correct-result";

        // Invoke the client
        var task = serviceLifetimeManager.InvokeConnectionAsync<string>(TestConnectionIds[1], "InvokedMethod", Array.Empty<object>(), default);

        // Check if the caller server sent a ClientInvocationMessage
        Assert.IsType<ClientInvocationMessage>(serviceConnectionManager.ServiceMessage);
        var invocation = (ClientInvocationMessage)serviceConnectionManager.ServiceMessage;

        // Check if the caller server added the invocation
        Assert.True(clientInvocationManager.Caller.TryGetInvocationReturnType(invocation.InvocationId, out _));

        // Complete the invocation by SerivceLifetimeManager
        var completionMessage = isCompletionWithResult
            ? CompletionMessage.WithResult(invocation.InvocationId, invocationResult)
            : CompletionMessage.WithError(invocation.InvocationId, invocationResult);

        await serviceLifetimeManager.SetConnectionResultAsync(invocation.ConnectionId, completionMessage);
        // Check if the caller server sent a ClientCompletionMessage
        Assert.IsType<ClientCompletionMessage>(serviceConnectionManager.ServiceMessage);

        // Check if the invocation result is correct
        try
        {
            await task;
            Assert.True(isCompletionWithResult);
            Assert.Equal(invocationResult, task.Result);
        }
        catch (Exception e)
        {
            Assert.False(isCompletionWithResult);
            Assert.Equal(invocationResult, e.Message);
        }
    }

    [Theory]
    [InlineData("json", true)]
    [InlineData("json", false)]
    [InlineData("messagepack", true)]
    [InlineData("messagepack", false)]
    public async void TestMultiClientInvocationsMultipleService(string protocol, bool isCompletionWithResult)
    {
        var clientConnectionContext = GetClientConnectionContextWithConnection(TestConnectionIds[1], protocol);
        var clientConnectionManager = new ClientConnectionManager();

        var serviceConnectionManager = new TestServiceConnectionManager<TestHub>();
        var clientInvocationManagers = new List<IClientInvocationManager>() {
            new DefaultClientInvocationManager(),
            new DefaultClientInvocationManager()
        };

        var serviceLifetimeManagers = new List<ServiceLifetimeManager<TestHub>>() {
            GetTestClientInvocationServiceLifetimeManager( new TestServiceConnection(), serviceConnectionManager, clientConnectionManager, clientInvocationManagers[0], null),
            GetTestClientInvocationServiceLifetimeManager( new TestServiceConnection(), serviceConnectionManager, clientConnectionManager, clientInvocationManagers[1], clientConnectionContext)
        };

        var invocationResult = "invocation-correct-result";

        // Invoke a client
        var task = serviceLifetimeManagers[0].InvokeConnectionAsync<string>(TestConnectionIds[1], "InvokedMethod", Array.Empty<object>());
        var invocation = (ClientInvocationMessage)serviceConnectionManager.ServiceMessage;
        // Check if the invocation was added to caller server
        Assert.True(clientInvocationManagers[0].Caller.TryGetInvocationReturnType(invocation.InvocationId, out _));

        // Route server adds invocation
        clientInvocationManagers[1].Router.AddInvocation(TestConnectionIds[1], invocation.InvocationId, "server-0", default);
        // check if the invocation was adder to route server
        Assert.True(clientInvocationManagers[1].Router.TryGetInvocationReturnType(invocation.InvocationId, out _));

        // The route server receives CompletionMessage
        var completionMessage = isCompletionWithResult
            ? CompletionMessage.WithResult(invocation.InvocationId, invocationResult)
            : CompletionMessage.WithError(invocation.InvocationId, invocationResult);
        await serviceLifetimeManagers[1].SetConnectionResultAsync(invocation.ConnectionId, completionMessage);

        // Check if the router server sent ClientCompletionMessage
        Assert.IsType<ClientCompletionMessage>(serviceConnectionManager.ServiceMessage);
        var clientCompletionMessage = (ClientCompletionMessage)serviceConnectionManager.ServiceMessage;

        clientInvocationManagers[0].Caller.TryCompleteResult(clientCompletionMessage.ConnectionId, clientCompletionMessage);

        try
        {
            await task;
            Assert.True(isCompletionWithResult);
            Assert.Equal(invocationResult, task.Result);
        }
        catch (Exception e)
        {
            Assert.False(isCompletionWithResult);
            Assert.Equal(invocationResult, e.Message);
        }
    }

    private static ServiceLifetimeManager<TestHub> GetTestClientInvocationServiceLifetimeManager(
        ServiceConnectionBase serviceConnection,
        IServiceConnectionManager<TestHub> serviceConnectionManager,
        ClientConnectionManager clientConnectionManager,
        IClientInvocationManager clientInvocationManager = null,
        ClientConnectionContext clientConnection = null)
    {
        // Add a client to ClientConnectionManager
        if (clientConnection != null)
        {
            clientConnection.ServiceConnection = serviceConnection;
            clientConnectionManager.TryAddClientConnection(clientConnection);
        }

        // Create ServiceLifetimeManager
        return new ServiceLifetimeManager<TestHub>(serviceConnectionManager,
                                                   clientConnectionManager,
                                                   HubProtocolResolver,
                                                   Logger,
                                                   Marker,
                                                   GlobalHubOptions,
                                                   LocalHubOptions,
                                                   null,
                                                   new DefaultServerNameProvider(),
                                                   clientInvocationManager ?? new DefaultClientInvocationManager());
    }

    private static ClientConnectionContext GetClientConnectionContextWithConnection(string connectionId = null, string protocol = null)
    {
        var connectMessage = new OpenConnectionMessage(connectionId, Array.Empty<Claim>());
        connectMessage.Protocol = protocol;
        return new ClientConnectionContext(connectMessage);
    }
}

#endif
