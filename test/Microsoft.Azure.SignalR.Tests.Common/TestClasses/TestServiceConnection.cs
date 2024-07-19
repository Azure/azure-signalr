// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.SignalR.Tests.Common;

internal class TestServiceConnection : ServiceConnectionBase
{
    private readonly TaskCompletionSource<object> _created = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly bool _throws;

    private ConnectionContext _connection;

    private ServiceConnectionStatus _expectedStatus;

    public IDuplexPipe Application { get; private set; }

    public Task ConnectionCreated => _created.Task;

    public ConcurrentQueue<ServiceMessage> ReceivedMessages { get; } = new();

    public TestServiceConnection(ServiceConnectionStatus status = ServiceConnectionStatus.Connected, bool throws = false,
        ILogger logger = null,
        IServiceMessageHandler serviceMessageHandler = null,
        IServiceEventHandler serviceEventHandler = null,
        IClientInvocationManager clientInvocationManager = null
        ) : base(
        new ServiceProtocol(),
        "serverId",
        Guid.NewGuid().ToString(),
        new TestHubServiceEndpoint(),
        serviceMessageHandler,
        serviceEventHandler,
        ServiceConnectionType.Default,
        logger ?? NullLogger.Instance
    )
    {
        _expectedStatus = status;
        _throws = throws;
    }

    public void SetStatus(ServiceConnectionStatus status)
    {
        Status = status;
        _expectedStatus = status;
    }

    public void Stop()
    {
        _connection?.Transport.Input.CancelPendingRead();
    }

    protected override Task CleanupClientConnections(string fromInstanceId = null)
    {
        return Task.CompletedTask;
    }

    protected override Task<ConnectionContext> CreateConnection(string target = null)
    {
        var pipeOptions = new PipeOptions();
        var duplex = DuplexPipe.CreateConnectionPair(pipeOptions, pipeOptions);

        Application = duplex.Application;
        _created.SetResult(null);

        return Task.FromResult<ConnectionContext>(new DefaultConnectionContext()
        {
            Application = duplex.Application,
            Transport = duplex.Transport
        });
    }

    protected override Task DisposeConnection(ConnectionContext connection)
    {
        return Task.CompletedTask;
    }

    protected override async Task<bool> HandshakeAsync(ConnectionContext connection)
    {
        _connection = connection;
        await Task.Yield();
        return _expectedStatus == ServiceConnectionStatus.Connected;
    }
    protected override Task OnClientConnectedAsync(OpenConnectionMessage openConnectionMessage)
    {
        return Task.CompletedTask;
    }

    protected override Task OnClientDisconnectedAsync(CloseConnectionMessage closeConnectionMessage)
    {
        return Task.CompletedTask;
    }

    protected override Task OnClientMessageAsync(ConnectionDataMessage connectionDataMessage)
    {
        return Task.CompletedTask;
    }

    protected override Task<bool> SafeWriteAsync(ServiceMessage serviceMessage)
    {
        if (_throws)
        {
            return Task.FromResult(false);
        }
        ReceivedMessages.Enqueue(serviceMessage);

        return Task.FromResult(true);
    }

    protected Task WriteAsyncBase(ServiceMessage serviceMessage)
    {
        return base.WriteAsync(serviceMessage);
    }
}
