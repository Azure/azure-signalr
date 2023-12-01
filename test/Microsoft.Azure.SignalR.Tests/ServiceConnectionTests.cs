// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

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
            using (StartVerifiableLog(out var loggerFactory))
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
                                                       "serverId", Guid.NewGuid().ToString("N"), null, null, null, new DefaultClientInvocationManager(), new AckHandler());

                var connectionTask = connection.StartAsync();

                // completed handshake
                await connection.ConnectionInitializedTask.OrTimeout();
                Assert.Equal(ServiceConnectionStatus.Connected, connection.Status);
                var clientConnectionId = Guid.NewGuid().ToString();

                var waitClientTask = ccm.WaitForClientConnectionAsync(clientConnectionId);
                await transportConnection.Application.Output.WriteAsync(
                    protocol.GetMessageBytes(new OpenConnectionMessage(clientConnectionId, new Claim[] { })));

                var clientConnection = await waitClientTask.OrTimeout();

                await transportConnection.Application.Output.WriteAsync(
                    protocol.GetMessageBytes(new CloseConnectionMessage(clientConnectionId)));

                // Normal end with close message
                await ccm.WaitForClientConnectionRemovalAsync(clientConnectionId).OrTimeout();

                // another connection comes in
                clientConnectionId = Guid.NewGuid().ToString();

                waitClientTask = ccm.WaitForClientConnectionAsync(clientConnectionId);
                await transportConnection.Application.Output.WriteAsync(
                    protocol.GetMessageBytes(new OpenConnectionMessage(clientConnectionId, new Claim[] { })));

                clientConnection = await waitClientTask.OrTimeout();

                // complete reading to end the connection
                transportConnection.Application.Output.Complete();

                await connectionTask.OrTimeout();
                Assert.Equal(ServiceConnectionStatus.Disconnected, connection.Status);
                Assert.Empty(ccm.ClientConnections);
            }
        }

        [Fact]
        public async Task TestServiceConnectionErrorCleansAllClients()
        {
            using (StartVerifiableLog(out var loggerFactory))
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
                                                       "serverId", Guid.NewGuid().ToString("N"), null, null, null, new DefaultClientInvocationManager(), new AckHandler());

                var connectionTask = connection.StartAsync();

                // completed handshake
                await connection.ConnectionInitializedTask.OrTimeout();
                Assert.Equal(ServiceConnectionStatus.Connected, connection.Status);
                var clientConnectionId = Guid.NewGuid().ToString();
                var waitClientTask = ccm.WaitForClientConnectionAsync(clientConnectionId);
                await transportConnection.Application.Output.WriteAsync(
                    protocol.GetMessageBytes(new OpenConnectionMessage(clientConnectionId, new Claim[] { })));

                var clientConnection = await waitClientTask.OrTimeout();
                // Cancel pending read to end the server connection
                transportConnection.Transport.Input.CancelPendingRead();

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
                    Assert.Equal("SendLoopStopped", logs[0].Write.EventId.Name);
                    Assert.Equal("ApplicationTaskFailed", logs[1].Write.EventId.Name);
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
                                                       "serverId", Guid.NewGuid().ToString("N"), null, null, null, new DefaultClientInvocationManager(), new AckHandler());

                var connectionTask = connection.StartAsync();

                // completed handshake
                await connection.ConnectionInitializedTask.OrTimeout();
                Assert.Equal(ServiceConnectionStatus.Connected, connection.Status);
                var clientConnectionId = Guid.NewGuid().ToString();
                var waitClientTask = ccm.WaitForClientConnectionAsync(clientConnectionId);
                await transportConnection.Application.Output.WriteAsync(
                    protocol.GetMessageBytes(new OpenConnectionMessage(clientConnectionId, new Claim[] { })));

                var clientConnection = await waitClientTask.OrTimeout();

                errorTcs.SetException(new InvalidOperationException("error operation"));

                await clientConnection.LifetimeTask.OrTimeout();

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
        public async Task TestServiceConnectionWithEndlessApplicationTaskNeverEnds()
        {
            var clientConnectionId = Guid.NewGuid().ToString();
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning, expectedErrors: c => true,
                logChecker: logs =>
                {
                    Assert.Single(logs);
                    Assert.Equal("DetectedLongRunningApplicationTask", logs[0].Write.EventId.Name);
                    Assert.Equal($"The connection {clientConnectionId} has a long running application logic that prevents the connection from complete.", logs[0].Write.Message);
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
                    "serverId", Guid.NewGuid().ToString("N"),
                    null, null, null, new DefaultClientInvocationManager(), new AckHandler(), closeTimeOutMilliseconds: 1);

                var connectionTask = connection.StartAsync();

                // completed handshake
                await connection.ConnectionInitializedTask.OrTimeout();
                Assert.Equal(ServiceConnectionStatus.Connected, connection.Status);
                var waitClientTask = ccm.WaitForClientConnectionAsync(clientConnectionId);
                await transportConnection.Application.Output.WriteAsync(
                    protocol.GetMessageBytes(new OpenConnectionMessage(clientConnectionId, new Claim[] { })));

                var clientConnection = await waitClientTask.OrTimeout();

                // complete reading to end the connection
                transportConnection.Application.Output.Complete();

                // Assert timeout
                var lifetime = clientConnection.LifetimeTask;
                var task = await Task.WhenAny(lifetime, Task.Delay(1000));
                Assert.NotEqual(lifetime, task);

                Assert.Equal(ServiceConnectionStatus.Disconnected, connection.Status);

                // since the service connection ends, the client connection is cleaned up from the collection...
                Assert.Empty(ccm.ClientConnections);
            }
        }

        [Fact]
        public async Task ClientConnectionOutgoingAbortCanEndLifeTime()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning, expectedErrors: c => true,
                logChecker: logs =>
                {
                    Assert.Single(logs);
                    Assert.Equal("SendLoopStopped", logs[0].Write.EventId.Name);
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
                                                       "serverId", Guid.NewGuid().ToString("N"), null, null, null, new DefaultClientInvocationManager(), new AckHandler(),
                                                       closeTimeOutMilliseconds: 500);

                var connectionTask = connection.StartAsync();

                // completed handshake
                await connection.ConnectionInitializedTask.OrTimeout();
                Assert.Equal(ServiceConnectionStatus.Connected, connection.Status);
                var clientConnectionId = Guid.NewGuid().ToString();
                var waitClientTask = ccm.WaitForClientConnectionAsync(clientConnectionId);
                await transportConnection.Application.Output.WriteAsync(
                    protocol.GetMessageBytes(new OpenConnectionMessage(clientConnectionId, new Claim[] { })));

                var clientConnection = await waitClientTask.OrTimeout();

                clientConnection.CancelOutgoing();

                connectionHandler.CancellationToken.Cancel();
                await clientConnection.LifetimeTask.OrTimeout();

                // complete reading to end the connection
                transportConnection.Application.Output.Complete();

                // 1s for application task to timeout
                await connectionTask.OrTimeout(1000);
                Assert.Equal(ServiceConnectionStatus.Disconnected, connection.Status);
                Assert.Empty(ccm.ClientConnections);
            }
        }

        [Fact]
        public async Task ClientConnectionContextAbortCanSendOutCloseMessage()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning, expectedErrors: c => true,
                logChecker: logs =>
                {
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
                var lastWill = "This is the last will";
                var connectionHandler = new LastWillConnectionHandler(lastWill);
                services.AddSingleton(connectionHandler);
                var builder = new ConnectionBuilder(services.BuildServiceProvider());
                builder.UseConnectionHandler<LastWillConnectionHandler>();
                ConnectionDelegate handler = builder.Build();

                var connection = new ServiceConnection(protocol, ccm, connectionFactory, loggerFactory, handler, ccf,
                    "serverId", Guid.NewGuid().ToString("N"), null, null, null, new DefaultClientInvocationManager(), new AckHandler(), closeTimeOutMilliseconds: 500);

                var connectionTask = connection.StartAsync();

                // completed handshake
                await connection.ConnectionInitializedTask.OrTimeout();
                Assert.Equal(ServiceConnectionStatus.Connected, connection.Status);
                var clientConnectionId = Guid.NewGuid().ToString();

                // make sure to register for wait first
                var waitClientTask = ccm.WaitForClientConnectionAsync(clientConnectionId);

                await transportConnection.Application.Output.WriteAsync(
                    protocol.GetMessageBytes(new OpenConnectionMessage(clientConnectionId, new Claim[] { })));
                var clientConnection = await waitClientTask.OrTimeout();

                await clientConnection.LifetimeTask.OrTimeout();

                transportConnection.Transport.Output.Complete();
                var input = await transportConnection.Application.Input.ReadAsync();
                var buffer = input.Buffer;
                var canParse = protocol.TryParseMessage(ref buffer, out var msg);
                Assert.True(canParse);
                var message = msg as ConnectionDataMessage;
                Assert.NotNull(message);

                Assert.Equal(clientConnectionId, message.ConnectionId);
                Assert.Equal(lastWill, Encoding.UTF8.GetString(message.Payload.First.ToArray()));

                // complete reading to end the connection
                transportConnection.Application.Output.Complete();

                // 1s for application task to timeout
                await connectionTask.OrTimeout(1000);
                Assert.Equal(ServiceConnectionStatus.Disconnected, connection.Status);
                Assert.Empty(ccm.ClientConnections);
            }
        }

        [Fact]
        public async Task ClientConnectionWithDiagnosticClientTagTest()
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

                var diagnosticClientConnectionId = "diagnosticClient";
                var normalClientConnectionId = "normalClient";

                var services = new ServiceCollection();
                var connectionHandler = new DiagnosticClientConnectionHandler(diagnosticClientConnectionId);
                services.AddSingleton(connectionHandler);
                var builder = new ConnectionBuilder(services.BuildServiceProvider());
                builder.UseConnectionHandler<DiagnosticClientConnectionHandler>();
                ConnectionDelegate handler = builder.Build();

                var connection = new ServiceConnection(protocol, ccm, connectionFactory, loggerFactory, handler, ccf,
                    "serverId", Guid.NewGuid().ToString("N"), null, null, null, new DefaultClientInvocationManager(), new AckHandler(), closeTimeOutMilliseconds: 500);

                var connectionTask = connection.StartAsync();

                // completed handshake
                await connection.ConnectionInitializedTask.OrTimeout();
                Assert.Equal(ServiceConnectionStatus.Connected, connection.Status);
                var waitClientTask = Task.WhenAll(ccm.WaitForClientConnectionAsync(normalClientConnectionId),
                    ccm.WaitForClientConnectionAsync(diagnosticClientConnectionId));
                await transportConnection.Application.Output.WriteAsync(
                        protocol.GetMessageBytes(new OpenConnectionMessage(diagnosticClientConnectionId, null, new Dictionary<string, StringValues>
                        {
                            { Constants.AsrsIsDiagnosticClient, "true"}
                        }, null)));

                await transportConnection.Application.Output.WriteAsync(
                    protocol.GetMessageBytes(new OpenConnectionMessage(normalClientConnectionId, null)));

                var connections = await waitClientTask.OrTimeout();
                await Task.WhenAll(from c in connections select c.LifetimeTask.OrTimeout());

                // complete reading to end the connection
                transportConnection.Application.Output.Complete();

                // 1s for application task to timeout
                await connectionTask.OrTimeout(1000);
                Assert.Equal(ServiceConnectionStatus.Disconnected, connection.Status);
                Assert.Empty(ccm.ClientConnections);
            }
        }

        [Fact]
        public async Task ClientConnectionLastWillCanSendOut()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning, expectedErrors: c => true,
                logChecker: logs =>
                {
                    Assert.Empty(logs);
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
                    "serverId", Guid.NewGuid().ToString("N"), null, null, null, new DefaultClientInvocationManager(), new AckHandler(), closeTimeOutMilliseconds: 500);

                var connectionTask = connection.StartAsync();

                // completed handshake
                await connection.ConnectionInitializedTask.OrTimeout();
                Assert.Equal(ServiceConnectionStatus.Connected, connection.Status);
                var clientConnectionId = Guid.NewGuid().ToString();

                var waitClientTask = ccm.WaitForClientConnectionAsync(clientConnectionId);
                await transportConnection.Application.Output.WriteAsync(
                    protocol.GetMessageBytes(new OpenConnectionMessage(clientConnectionId, new Claim[] { })));

                var clientConnection = await waitClientTask.OrTimeout();

                // complete reading to end the connection
                transportConnection.Application.Output.Complete();

                connectionHandler.CancellationToken.Cancel();

                await clientConnection.LifetimeTask.OrTimeout();

                // 1s for application task to timeout
                await connectionTask.OrTimeout(1000);
                Assert.Equal(ServiceConnectionStatus.Disconnected, connection.Status);
                Assert.Empty(ccm.ClientConnections);
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

        private sealed class LastWillConnectionHandler : ConnectionHandler
        {
            private readonly string _lastWill;

            public LastWillConnectionHandler(string lastWill)
            {
                _lastWill = lastWill;
            }

            public override async Task OnConnectedAsync(ConnectionContext connection)
            {
                await connection.Transport.Output.WriteAsync(Encoding.UTF8.GetBytes(_lastWill));
            }
        }

        private sealed class EndlessConnectionHandler : ConnectionHandler
        {
            public CancellationTokenSource CancellationToken { get; }

            public EndlessConnectionHandler()
            {
                CancellationToken = new CancellationTokenSource();
            }

            public EndlessConnectionHandler(CancellationTokenSource token)
            {
                CancellationToken = token;
            }
            public override async Task OnConnectedAsync(ConnectionContext connection)
            {
                while (!CancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(100);
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

        private sealed class DiagnosticClientConnectionHandler : ConnectionHandler
        {
            private string _diagnosticClient;

            public DiagnosticClientConnectionHandler(string diagnosticClient)
            {
                _diagnosticClient = diagnosticClient;
            }

            public override Task OnConnectedAsync(ConnectionContext connection)
            {
                Assert.Equal(ClientConnectionScope.IsDiagnosticClient, connection.ConnectionId == _diagnosticClient);
                return Task.CompletedTask;
            }
        }

        internal sealed class TestClientConnectionManager : IClientConnectionManager
        {
            private readonly ClientConnectionManager _ccm = new ClientConnectionManager();

            private readonly ConcurrentDictionary<string, TaskCompletionSource<ClientConnectionContext>> _tcs =
                new ConcurrentDictionary<string, TaskCompletionSource<ClientConnectionContext>>();

            private readonly ConcurrentDictionary<string, TaskCompletionSource<ClientConnectionContext>> _tcsForRemoval
                = new ConcurrentDictionary<string, TaskCompletionSource<ClientConnectionContext>>();

            public TestClientConnectionManager()
            {
            }

            public Task<ClientConnectionContext> WaitForClientConnectionRemovalAsync(string id)
            {
                var tcs = _tcsForRemoval.GetOrAdd(id,
                    s => new TaskCompletionSource<ClientConnectionContext>(TaskCreationOptions
                        .RunContinuationsAsynchronously));
                return tcs.Task;
            }

            public Task<ClientConnectionContext> WaitForClientConnectionAsync(string id)
            {
                var tcs = _tcs.GetOrAdd(id,
                    s => new TaskCompletionSource<ClientConnectionContext>(TaskCreationOptions
                        .RunContinuationsAsynchronously));
                return tcs.Task;
            }

            public bool TryAddClientConnection(ClientConnectionContext connection)
            {
                var tcs = _tcs.GetOrAdd(connection.ConnectionId,
                    s => new TaskCompletionSource<ClientConnectionContext>(TaskCreationOptions
                        .RunContinuationsAsynchronously));
                var r = _ccm.TryAddClientConnection(connection);
                tcs.SetResult(connection);
                return r;
            }

            public bool TryRemoveClientConnection(string connectionId, out ClientConnectionContext connection)
            {
                var tcs = _tcsForRemoval.GetOrAdd(connectionId,
                    s => new TaskCompletionSource<ClientConnectionContext>(TaskCreationOptions
                        .RunContinuationsAsynchronously));
                _tcs.TryRemove(connectionId, out _);
                var r = _ccm.TryRemoveClientConnection(connectionId, out connection);
                tcs.TrySetResult(connection);
                return r;
            }

            public Task WhenAllCompleted()
            {
                return Task.CompletedTask;
            }

            public IReadOnlyDictionary<string, ClientConnectionContext> ClientConnections => _ccm.ClientConnections;
        }
    }
}
