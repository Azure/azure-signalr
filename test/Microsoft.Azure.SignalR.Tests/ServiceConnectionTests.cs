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
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
                                                       "serverId", Guid.NewGuid().ToString("N"), null, null);

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
        public async Task TestServiceConnectionErrorCleansAllClients()
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
                                                       "serverId", Guid.NewGuid().ToString("N"), null, null);

                var connectionTask = connection.StartAsync();

                // completed handshake
                await connection.ConnectionInitializedTask.OrTimeout();
                Assert.Equal(ServiceConnectionStatus.Connected, connection.Status);
                var clientConnectionId = Guid.NewGuid().ToString();

                await transportConnection.Application.Output.WriteAsync(
                    protocol.GetMessageBytes(new OpenConnectionMessage(clientConnectionId, new Claim[] { })));

                var clientConnection = await ccm.WaitForClientConnectionAsync(clientConnectionId).OrTimeout();
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
                                                       "serverId", Guid.NewGuid().ToString("N"), null, null);

                var connectionTask = connection.StartAsync();

                // completed handshake
                await connection.ConnectionInitializedTask.OrTimeout();
                Assert.Equal(ServiceConnectionStatus.Connected, connection.Status);
                var clientConnectionId = Guid.NewGuid().ToString();

                await transportConnection.Application.Output.WriteAsync(
                    protocol.GetMessageBytes(new OpenConnectionMessage(clientConnectionId, new Claim[] { })));

                var clientConnection = await ccm.WaitForClientConnectionAsync(clientConnectionId).OrTimeout();

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
        public async Task TestServiceConnectionWithEndlessApplicationTask()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning, expectedErrors: c => true,
                logChecker: logs =>
                {
                    Assert.Single(logs);
                    Assert.Equal("ApplicationTaskFailed", logs[0].Write.EventId.Name);
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
                    null, null, closeTimeOutMilliseconds: 1000);

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

                await clientConnection.LifetimeTask.OrTimeout();

                // 500ms for application task to timeout
                await connectionTask.OrTimeout(1000);
                Assert.Equal(ServiceConnectionStatus.Disconnected, connection.Status);
                Assert.Empty(ccm.ClientConnections);

                connectionHandler.CancellationToken.Cancel();
            }
        }

        [Fact]
        public async Task ClientConnectionOutgoingAbortCanEndLifeTime()
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
                var connectionHandler = new EndlessConnectionHandler();
                services.AddSingleton(connectionHandler);
                var builder = new ConnectionBuilder(services.BuildServiceProvider());
                builder.UseConnectionHandler<EndlessConnectionHandler>();
                ConnectionDelegate handler = builder.Build();
                var connection = new ServiceConnection(protocol, ccm, connectionFactory, loggerFactory, handler, ccf,
                                                       "serverId", Guid.NewGuid().ToString("N"), null, null,
                                                       closeTimeOutMilliseconds: 500);

                var connectionTask = connection.StartAsync();

                // completed handshake
                await connection.ConnectionInitializedTask.OrTimeout();
                Assert.Equal(ServiceConnectionStatus.Connected, connection.Status);
                var clientConnectionId = Guid.NewGuid().ToString();

                await transportConnection.Application.Output.WriteAsync(
                    protocol.GetMessageBytes(new OpenConnectionMessage(clientConnectionId, new Claim[] { })));

                var clientConnection = await ccm.WaitForClientConnectionAsync(clientConnectionId).OrTimeout();

                clientConnection.CancelOutgoing();

                await clientConnection.LifetimeTask.OrTimeout();

                // complete reading to end the connection
                transportConnection.Application.Output.Complete();

                // 1s for application task to timeout
                await connectionTask.OrTimeout(1000);
                Assert.Equal(ServiceConnectionStatus.Disconnected, connection.Status);
                Assert.Empty(ccm.ClientConnections);

                connectionHandler.CancellationToken.Cancel();
            }
        }

        [Fact]
        public async Task ClientConnectionContextAbortCanSendOutCloseMessage()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Trace, expectedErrors: c => true,
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
                    "serverId", Guid.NewGuid().ToString("N"), null, null, closeTimeOutMilliseconds: 500);

                var connectionTask = connection.StartAsync();

                // completed handshake
                await connection.ConnectionInitializedTask.OrTimeout();
                Assert.Equal(ServiceConnectionStatus.Connected, connection.Status);
                var clientConnectionId = Guid.NewGuid().ToString();

                await transportConnection.Application.Output.WriteAsync(
                    protocol.GetMessageBytes(new OpenConnectionMessage(clientConnectionId, new Claim[] { })));

                var clientConnection = await ccm.WaitForClientConnectionAsync(clientConnectionId).OrTimeout();

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
        public async Task ClientConnectionLastWillCanSendOut()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning, expectedErrors: c => true,
                logChecker: logs =>
                {
                    Assert.Single(logs);
                    Assert.Equal("ApplicationTaskFailed", logs[0].Write.EventId.Name);
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
                    "serverId", Guid.NewGuid().ToString("N"), null, null, closeTimeOutMilliseconds: 500);

                var connectionTask = connection.StartAsync();

                // completed handshake
                await connection.ConnectionInitializedTask.OrTimeout();
                Assert.Equal(ServiceConnectionStatus.Connected, connection.Status);
                var clientConnectionId = Guid.NewGuid().ToString();

                await transportConnection.Application.Output.WriteAsync(
                    protocol.GetMessageBytes(new OpenConnectionMessage(clientConnectionId, new Claim[] { })));

                var clientConnection = await ccm.WaitForClientConnectionAsync(clientConnectionId).OrTimeout();

                clientConnection.CancelApplication();

                // complete reading to end the connection
                transportConnection.Application.Output.Complete();

                await clientConnection.LifetimeTask.OrTimeout();

                // 1s for application task to timeout
                await connectionTask.OrTimeout(1000);
                Assert.Equal(ServiceConnectionStatus.Disconnected, connection.Status);
                Assert.Empty(ccm.ClientConnections);

                connectionHandler.CancellationToken.Cancel();
            }
        }

        [Fact]
        public async Task TestServiceConnectionForMigratedIn()
        {
            var factory = new TestClientConnectionFactory();
            var connection = MockServiceConnection(null, factory);

            // create a connection with migration header.
            await connection.OnClientConnectedAsyncForTest(new OpenConnectionMessage("foo", new Claim[0])
            {
                Headers = new Dictionary<string, StringValues>{
                    { Constants.AsrsMigrateFrom, "another-server" }
                }
            });

            Assert.Equal(1, factory.Connections.Count);
            var context = factory.Connections[0];
            Assert.True(context.IsMigrated);

            var message = new AspNetCore.SignalR.Protocol.HandshakeResponseMessage("");
            HandshakeProtocol.WriteResponseMessage(message, context.Transport.Output);
            await context.Transport.Output.FlushAsync();

            var task = context.Transport.Input.ReadAsync();
            await Task.Delay(100);

            // nothing should be written into the transport
            Assert.False(task.IsCompleted);
            Assert.True(context.IsMigrated);

            var feature = context.Features.Get<IConnectionMigrationFeature>();
            Assert.NotNull(feature);
            Assert.Equal("another-server", feature.MigrateFrom);
        }

        [Fact]
        public async Task TestServiceConnectionForMigratedOut()
        {
            var factory = new TestClientConnectionFactory();

            var connection = MockServiceConnection(null, factory, mode: GracefulShutdownMode.MigrateClients);

            // create a connection with migration header.
            await connection.OnClientConnectedAsyncForTest(new OpenConnectionMessage("foo", new Claim[0]));

            var context = factory.Connections[0];

            var closeMessage = new CloseConnectionMessage(context.ConnectionId);
            closeMessage.Headers.Add(Constants.AsrsMigrateTo, "another-server");

            await connection.OnClientDisconnectedAsyncForTest(closeMessage);

            var feature = context.Features.Get<IConnectionMigrationFeature>();
            Assert.NotNull(feature);
            Assert.Equal("another-server", feature.MigrateTo);
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

        private sealed class TestServiceConnection : ServiceConnection
        {
            public TestServiceConnection(IConnectionFactory serviceConnectionFactory,
                                         IClientConnectionFactory clientConnectionFactory,
                                         ILoggerFactory loggerFactory,
                                         ConnectionDelegate handler,
                                         GracefulShutdownMode mode = GracefulShutdownMode.Off) : base(
                new ServiceProtocol(),
                new TestClientConnectionManager(),
                serviceConnectionFactory,
                loggerFactory,
                handler,
                clientConnectionFactory,
                "serverId",
                Guid.NewGuid().ToString("N"),
                null,
                null,
                mode: mode
            )
            {
            }

            public Task OnClientConnectedAsyncForTest(OpenConnectionMessage message)
            {
                return base.OnClientConnectedAsync(message);
            }

            public Task OnClientDisconnectedAsyncForTest(CloseConnectionMessage message)
            {
                return base.OnClientDisconnectedAsync(message);
            }
        }

        private TestServiceConnection MockServiceConnection(IConnectionFactory serviceConnectionFactory = null,
                                                            IClientConnectionFactory clientConnectionFactory = null,
                                                            ILoggerFactory loggerFactory = null,
                                                            GracefulShutdownMode mode = GracefulShutdownMode.Off)
        {
            clientConnectionFactory ??= new ClientConnectionFactory();
            serviceConnectionFactory ??= new TestConnectionFactory(conn => Task.CompletedTask);
            loggerFactory ??= NullLoggerFactory.Instance;

            var services = new ServiceCollection();
            var connectionHandler = new EndlessConnectionHandler();
            services.AddSingleton(connectionHandler);
            var builder = new ConnectionBuilder(services.BuildServiceProvider());
            builder.UseConnectionHandler<EndlessConnectionHandler>();
            ConnectionDelegate handler = builder.Build();

            return new TestServiceConnection(
                serviceConnectionFactory,
                clientConnectionFactory,
                loggerFactory,
                handler,
                mode: mode
            );
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

            public void AddClientConnection(ClientConnectionContext clientConnection)
            {
                var tcs = _tcs.GetOrAdd(clientConnection.ConnectionId,
                    s => new TaskCompletionSource<ClientConnectionContext>(TaskCreationOptions
                        .RunContinuationsAsynchronously));
                _ccm.AddClientConnection(clientConnection);
                tcs.SetResult(clientConnection);
            }

            public ClientConnectionContext RemoveClientConnection(string connectionId)
            {
                var tcs = _tcsForRemoval.GetOrAdd(connectionId,
                    s => new TaskCompletionSource<ClientConnectionContext>(TaskCreationOptions
                        .RunContinuationsAsynchronously));
                _tcs.TryRemove(connectionId, out _);
                var connection = _ccm.RemoveClientConnection(connectionId);
                tcs.TrySetResult(connection);
                return connection;
            }

            public Task WhenAllCompleted()
            {
                return Task.CompletedTask;
            }

            public IReadOnlyDictionary<string, ClientConnectionContext> ClientConnections => _ccm.ClientConnections;
        }
    }
}
