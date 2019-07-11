// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Tests
{
    public class ServiceConnectionTests : VerifiableLoggedTest
    {
        public ServiceConnectionTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task TestServiceConnectionWithNormalApplicationTask()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {
                var ccm = new TestClientConnectionManager();
                var ccf = new ClientConnectionFactory();
                var protocol = new ServiceProtocol();
                TestConnection transportConnection = null;
                var connectionFactory = new TestConnectionFactory(conn =>
                {
                    transportConnection = conn;
                    return Task.CompletedTask;
                });
                var services = new ServiceCollection();
                var builder = new ConnectionBuilder(services.BuildServiceProvider());
                builder.UseConnectionHandler<TestConnectionHandler>();
                ConnectionDelegate handler = builder.Build();
                var connection = new ServiceConnection(protocol, ccm, connectionFactory, loggerFactory, handler, ccf,
                    Guid.NewGuid().ToString("N"), null, null);

                var connectionTask = connection.StartAsync();

                // completed handshake
                await connection.ConnectionInitializedTask.OrTimeout();
                Assert.Equal(ServiceConnectionStatus.Connected, connection.Status);
                var clientConnectionId = Guid.NewGuid().ToString();

                await transportConnection.Application.Output.WriteAsync(
                    protocol.GetMessageBytes(new OpenConnectionMessage(clientConnectionId, new Claim[] { })));

                var clientConnection = await ccm.WaitForClientConnectionAsync(clientConnectionId).OrTimeout();

                await transportConnection.Application.Output.WriteAsync(
                    protocol.GetMessageBytes(new CloseConnectionMessage(clientConnectionId)));

                // Normal end with close message
                await ccm.WaitForClientConnectionRemovalAsync(clientConnectionId).OrTimeout();

                // another connection comes in
                clientConnectionId = Guid.NewGuid().ToString();

                await transportConnection.Application.Output.WriteAsync(
                    protocol.GetMessageBytes(new OpenConnectionMessage(clientConnectionId, new Claim[] { })));

                clientConnection = await ccm.WaitForClientConnectionAsync(clientConnectionId).OrTimeout();

                // complete reading to end the connection
                transportConnection.Application.Output.Complete();

                await connectionTask.OrTimeout();
                Assert.Equal(ServiceConnectionStatus.Disconnected, connection.Status);
                Assert.Empty(ccm.ClientConnections);
            }
        }

        [Fact]
        public async Task TestServiceConnectionWithErrorApplicationTask()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning, expectedErrors: c => true,
                logChecker: logs =>
                {
                    Assert.Equal(2, logs.Count);
                    Assert.Equal("ApplicationTaskFailed", logs[0].Write.EventId.Name);
                    Assert.Equal("SendLoopStopped", logs[1].Write.EventId.Name);
                    return true;
                }))
            {
                var ccm = new TestClientConnectionManager();
                var ccf = new ClientConnectionFactory();
                var protocol = new ServiceProtocol();
                TestConnection transportConnection = null;
                var connectionFactory = new TestConnectionFactory(conn =>
                {
                    transportConnection = conn;
                    return Task.CompletedTask;
                });
                var services = new ServiceCollection();
                var errorTcs = new TaskCompletionSource<Exception>();
                var connectionHandler = new ErrorConnectionHandler(errorTcs);
                services.AddSingleton(connectionHandler);
                var builder = new ConnectionBuilder(services.BuildServiceProvider());

                builder.UseConnectionHandler<ErrorConnectionHandler>();
                ConnectionDelegate handler = builder.Build();

                var connection = new ServiceConnection(protocol, ccm, connectionFactory, loggerFactory, handler, ccf,
                    Guid.NewGuid().ToString("N"), null, null);

                var connectionTask = connection.StartAsync();

                // completed handshake
                await connection.ConnectionInitializedTask.OrTimeout();
                Assert.Equal(ServiceConnectionStatus.Connected, connection.Status);
                var clientConnectionId = Guid.NewGuid().ToString();

                await transportConnection.Application.Output.WriteAsync(
                    protocol.GetMessageBytes(new OpenConnectionMessage(clientConnectionId, new Claim[] { })));

                var clientConnection = await ccm.WaitForClientConnectionAsync(clientConnectionId).OrTimeout();

                errorTcs.SetException(new InvalidOperationException("error operation"));

                // Should complete the connection when application throws
                await ccm.WaitForClientConnectionRemovalAsync(clientConnectionId).OrTimeout();

                // Application task should not affect the underlying service connection
                Assert.Equal(ServiceConnectionStatus.Connected, connection.Status);

                // complete reading to end the connection
                transportConnection.Application.Output.Complete();

                await connectionTask.OrTimeout();
                Assert.Equal(ServiceConnectionStatus.Disconnected, connection.Status);
                Assert.Empty(ccm.ClientConnections);
            }
        }

        [Fact]
        public async Task TestServiceConnectionWithEndlessApplicationTask()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning, expectedErrors: c => true,
                logChecker: logs =>
                {
                    Assert.Single(logs);
                    Assert.Equal("ApplicationTaskTimedOut", logs[0].Write.EventId.Name);
                    return true;
                }))
            {
                var ccm = new TestClientConnectionManager();
                var ccf = new ClientConnectionFactory();
                var protocol = new ServiceProtocol();
                TestConnection transportConnection = null;
                var connectionFactory = new TestConnectionFactory(conn =>
                {
                    transportConnection = conn;
                    return Task.CompletedTask;
                });
                var services = new ServiceCollection();
                var connectionHandler = new EndlessConnectionHandler();
                services.AddSingleton(connectionHandler);
                var builder = new ConnectionBuilder(services.BuildServiceProvider());
                builder.UseConnectionHandler<EndlessConnectionHandler>();
                ConnectionDelegate handler = builder.Build();
                var connection = new ServiceConnection(protocol, ccm, connectionFactory, loggerFactory, handler, ccf,
                    Guid.NewGuid().ToString("N"), null, null);

                var connectionTask = connection.StartAsync();

                // completed handshake
                await connection.ConnectionInitializedTask.OrTimeout();
                Assert.Equal(ServiceConnectionStatus.Connected, connection.Status);
                var clientConnectionId = Guid.NewGuid().ToString();

                await transportConnection.Application.Output.WriteAsync(
                    protocol.GetMessageBytes(new OpenConnectionMessage(clientConnectionId, new Claim[] { })));

                var clientConnection = await ccm.WaitForClientConnectionAsync(clientConnectionId).OrTimeout();

                // complete reading to end the connection
                transportConnection.Application.Output.Complete();

                // 5 seconds for application task to timeout
                await connectionTask.OrTimeout(10000);
                Assert.Equal(ServiceConnectionStatus.Disconnected, connection.Status);
                Assert.Empty(ccm.ClientConnections);

                connectionHandler.CancellationToken.Cancel();
            }
        }

        private sealed class TestConnectionHandler : ConnectionHandler
        {
            private TaskCompletionSource<object> _startedTcs = new TaskCompletionSource<object>();

            public Task Started => _startedTcs.Task;

            public override async Task OnConnectedAsync(ConnectionContext connection)
            {
                _startedTcs.TrySetResult(null);

                while (true)
                {
                    var result = await connection.Transport.Input.ReadAsync();

                    try
                    {
                        if (result.IsCompleted)
                        {
                            break;
                        }
                    }
                    finally
                    {
                        connection.Transport.Input.AdvanceTo(result.Buffer.End);
                    }
                }
            }
        }

        private sealed class EndlessConnectionHandler : ConnectionHandler
        {
            public CancellationTokenSource CancellationToken { get; } = new CancellationTokenSource();

            public override async Task OnConnectedAsync(ConnectionContext connection)
            {
                while (!CancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(1000);
                }
            }
        }

        private sealed class ErrorConnectionHandler : ConnectionHandler
        {
            private readonly TaskCompletionSource<Exception> _throwTcs;

            public ErrorConnectionHandler(TaskCompletionSource<Exception> throwTcs)
            {
                _throwTcs = throwTcs;
            }

            public override async Task OnConnectedAsync(ConnectionContext connection)
            {
                var ex = await _throwTcs.Task;
                throw ex;
            }
        }

        private sealed class TestClientConnectionManager : IClientConnectionManager
        {
            private readonly ClientConnectionManager _ccm = new ClientConnectionManager();

            private readonly ConcurrentDictionary<string, TaskCompletionSource<ServiceConnectionContext>> _tcs =
                new ConcurrentDictionary<string, TaskCompletionSource<ServiceConnectionContext>>();

            private readonly ConcurrentDictionary<string, TaskCompletionSource<ServiceConnectionContext>> _tcsForRemoval
                = new ConcurrentDictionary<string, TaskCompletionSource<ServiceConnectionContext>>();

            public Task<ServiceConnectionContext> WaitForClientConnectionRemovalAsync(string id)
            {
                var tcs = _tcsForRemoval.GetOrAdd(id,
                    s => new TaskCompletionSource<ServiceConnectionContext>(TaskCreationOptions
                        .RunContinuationsAsynchronously));
                return tcs.Task;
            }

            public Task<ServiceConnectionContext> WaitForClientConnectionAsync(string id)
            {
                var tcs = _tcs.GetOrAdd(id,
                    s => new TaskCompletionSource<ServiceConnectionContext>(TaskCreationOptions
                        .RunContinuationsAsynchronously));
                return tcs.Task;
            }

            public void AddClientConnection(ServiceConnectionContext clientConnection)
            {
                var tcs = _tcs.GetOrAdd(clientConnection.ConnectionId,
                    s => new TaskCompletionSource<ServiceConnectionContext>(TaskCreationOptions
                        .RunContinuationsAsynchronously));
                _ccm.AddClientConnection(clientConnection);
                tcs.SetResult(clientConnection);
            }

            public ServiceConnectionContext RemoveClientConnection(string connectionId)
            {
                var tcs = _tcsForRemoval.GetOrAdd(connectionId,
                    s => new TaskCompletionSource<ServiceConnectionContext>(TaskCreationOptions
                        .RunContinuationsAsynchronously));
                _tcs.TryRemove(connectionId, out _);
                var connection = _ccm.RemoveClientConnection(connectionId);
                tcs.SetResult(connection);
                return connection;
            }

            public IReadOnlyDictionary<string, ServiceConnectionContext> ClientConnections => _ccm.ClientConnections;
        }
    }
}
