// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.AspNet.Tests;

internal class TestServiceConnectionProxy(IClientConnectionManager clientConnectionManager,
                                          ILoggerFactory loggerFactory,
                                          IServiceMessageHandler serviceMessageHandler = null) : ServiceConnection(
                                              "serverId",
                                              Guid.NewGuid().ToString("N"),
                                              null,
                                              SharedServiceProtocol,
                                              new TestConnectionFactory(),
                                              clientConnectionManager,
                                              loggerFactory,
                                              serviceMessageHandler ?? new TestServiceMessageHandler(),
                                              null,
                                              new AckHandler()), IDisposable
{
    private static readonly ServiceProtocol SharedServiceProtocol = new ServiceProtocol();

    private readonly ConcurrentDictionary<string, TaskCompletionSource<ServiceMessage>> _waitForOutgoingMessage = new ConcurrentDictionary<string, TaskCompletionSource<ServiceMessage>>();

    private readonly TaskCompletionSource<object> _connectionClosedTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

    public TestConnectionContext TestConnectionContext { get; private set; }

    public Task WaitForConnectionClose => _connectionClosedTcs.Task;

    public async Task StartServiceAsync()
    {
        _ = StartAsync();

        await ConnectionInitializedTask;
    }

    public Task<ServiceMessage> WaitForOutgoingMessageAsync(string connectionId)
    {
        var tcs = _waitForOutgoingMessage.GetOrAdd(connectionId, i => new TaskCompletionSource<ServiceMessage>(TaskCreationOptions.RunContinuationsAsynchronously));

        return tcs.Task;
    }

    public async Task WriteMessageAsync(ServiceMessage message)
    {
        if (TestConnectionContext == null)
        {
            throw new InvalidOperationException("Server connection is not yet established.");
        }

        ServiceProtocol.WriteMessage(message, TestConnectionContext.Application.Output);
        await TestConnectionContext.Application.Output.FlushAsync();
    }

    public void Dispose()
    {
        _ = StopAsync();
    }

    protected override async Task<ConnectionContext> CreateConnection(string target = null)
    {
        TestConnectionContext = await base.CreateConnection() as TestConnectionContext;

        await WriteMessageAsync(new HandshakeResponseMessage());
        return TestConnectionContext;
    }

    protected override async Task CleanupConnectionsAsyncCore(string instanceId = null)
    {
        try
        {
            await base.CleanupConnectionsAsyncCore(instanceId);
            _connectionClosedTcs.SetResult(null);
        }
        catch (Exception e)
        {
            _connectionClosedTcs.SetException(e);
        }
    }

    protected override async Task<bool> SafeWriteAsync(ServiceMessage serviceMessage)
    {
        var result = await base.SafeWriteAsync(serviceMessage);

        if (serviceMessage is ConnectionDataMessage cdm)
        {
            var tcs = _waitForOutgoingMessage.GetOrAdd(cdm.ConnectionId, t => new TaskCompletionSource<ServiceMessage>(TaskCreationOptions.RunContinuationsAsynchronously));
            tcs.TrySetResult(serviceMessage);
        }
        else if (serviceMessage is CloseConnectionMessage ccm)
        {
            var tcs = _waitForOutgoingMessage.GetOrAdd(ccm.ConnectionId, t => new TaskCompletionSource<ServiceMessage>(TaskCreationOptions.RunContinuationsAsynchronously));
            tcs.TrySetResult(serviceMessage);
        }
        return result;
    }
}
