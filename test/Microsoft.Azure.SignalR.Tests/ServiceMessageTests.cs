// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;
using Xunit.Abstractions;

using static Microsoft.Azure.SignalR.Tests.ServiceConnectionTests;

using SignalRProtocol = Microsoft.AspNetCore.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.Tests
{
    public class ServiceMessageTests : VerifiableLoggedTest
    {
        private static readonly AccessKeyResponseMessage Error = new AccessKeyResponseMessage()
        {
            ErrorType = nameof(ArgumentException),
            ErrorMessage = "This is a error messsage"
        };

        private static readonly AccessKeyResponseMessage Normal = new AccessKeyResponseMessage()
        {
            Kid = "foo",
            AccessKey = "This is a long long key",
        };

        public ServiceMessageTests(ITestOutputHelper output) : base(output)
        {
        }

        [Theory]
        [InlineData("normal", 0)]
        [InlineData("error", 1)]
        public async Task TestHandleAccessKeyMessage(string messageType, int logCount)
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Error, expectedErrors: c => logCount > 0,
                logChecker: logs =>
                {
                    Assert.Equal(logCount, logs.Count);
                    return true;
                }))
            {
                var conn = CreateServiceConnection(loggerFactory: loggerFactory);
                var ccm = conn.ClientConnectionManager;

                var connectionTask = conn.StartAsync();

                // completed handshake
                await conn.ConnectionInitializedTask.OrTimeout();
                Assert.Equal(ServiceConnectionStatus.Connected, conn.Status);

                var message = messageType switch
                {
                    "normal" => Normal,
                    "error" => Error,
                    _ => throw new NotImplementedException(),
                };
                await conn.WriteFromServiceAsync(message);

                // complete reading to end the connection
                conn.CompleteWriteFromService();

                await connectionTask.OrTimeout();
                Assert.Equal(ServiceConnectionStatus.Disconnected, conn.Status);
                Assert.Empty(ccm.ClientConnections);
            }
        }

        [Fact]
        public async Task TestOpenConnectionMessageWithMigrateIn()
        {
            var clientConnectionFactory = new TestClientConnectionFactory();

            var connection = CreateServiceConnection(clientConnectionFactory: clientConnectionFactory);
            _ = connection.StartAsync();
            _ = connection.WriteFromServiceAsync(new HandshakeResponseMessage());

            var openConnectionMessage = new OpenConnectionMessage("foo", Array.Empty<Claim>());
            openConnectionMessage.Headers.Add(Constants.AsrsMigrateFrom, "another-server");
            _ = connection.WriteFromServiceAsync(openConnectionMessage);
            await connection.ConnectedTask;

            Assert.Equal(1, clientConnectionFactory.Connections.Count);
            var clientConnection = clientConnectionFactory.Connections[0];
            Assert.True(clientConnection.IsMigrated);

            // write a handshake response
            var message = new SignalRProtocol.HandshakeResponseMessage("");
            SignalRProtocol.HandshakeProtocol.WriteResponseMessage(message, clientConnection.Transport.Output);
            await clientConnection.Transport.Output.FlushAsync();

            var task = clientConnection.Transport.Input.ReadAsync();
            await Task.Delay(100);

            // nothing should be written into the transport
            Assert.False(task.IsCompleted);
            Assert.True(clientConnection.IsMigrated);

            var feature = clientConnection.Features.Get<IConnectionMigrationFeature>();
            Assert.NotNull(feature);
            Assert.Equal("another-server", feature.MigrateFrom);

            await connection.StopAsync();
        }

        [Fact]
        public async Task TestCloseConnectionMessageWithMigrateOut()
        {
            var clientConnectionFactory = new TestClientConnectionFactory();

            var connection = CreateServiceConnection(clientConnectionFactory: clientConnectionFactory, handler: new TestConnectionHandler(3000, "foobar"));
            _ = connection.StartAsync();
            _ = connection.WriteFromServiceAsync(new HandshakeResponseMessage());

            var openConnectionMessage = new OpenConnectionMessage("foo", Array.Empty<Claim>());
            _ = connection.WriteFromServiceAsync(openConnectionMessage);
            await connection.ConnectedTask;

            Assert.Equal(1, clientConnectionFactory.Connections.Count);
            var clientConnection = clientConnectionFactory.Connections[0];
            Assert.False(clientConnection.IsMigrated);

            // write a signalr handshake response
            var message = new SignalRProtocol.HandshakeResponseMessage("");
            SignalRProtocol.HandshakeProtocol.WriteResponseMessage(message, clientConnection.Transport.Output);
            await clientConnection.Transport.Output.FlushAsync();

            // write a close connection message with migration header
            var closeMessage = new CloseConnectionMessage(clientConnection.ConnectionId);
            closeMessage.Headers.Add(Constants.AsrsMigrateTo, "another-server");
            await connection.WriteFromServiceAsync(closeMessage);

            // wait until app task completed.
            // await Assert.ThrowsAsync<TimeoutException>(async () => await clientConnection.LifetimeTask.OrTimeout(1000));
            await clientConnection.LifetimeTask;

            // expect a handshake response message.
            using (var writer = new MemoryBufferWriter())
            {
                SignalRProtocol.HandshakeProtocol.WriteResponseMessage(SignalRProtocol.HandshakeResponseMessage.Empty, writer);
                var dataMessage = new ConnectionDataMessage(clientConnection.ConnectionId, writer.ToArray());
                await connection.ExpectConnectionDataMessage(dataMessage);
            }

            // signalr close message should be skipped.
            using (var writer = new MemoryBufferWriter())
            {
                var protocol = new SignalRProtocol.JsonHubProtocol();
                protocol.WriteMessage(SignalRProtocol.CloseMessage.Empty, writer);
                var dataMessage = new ConnectionDataMessage(clientConnection.ConnectionId, writer.ToArray());
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await Assert.ThrowsAsync<OperationCanceledException>(async () => await connection.ExpectConnectionDataMessage(dataMessage, cts.Token));
            }

            var feature = clientConnection.Features.Get<IConnectionMigrationFeature>();
            Assert.NotNull(feature);
            Assert.Equal("another-server", feature.MigrateTo);

            await connection.StopAsync();
        }

        [Fact]
        public async Task TestCloseConnectionMessage()
        {
            var clientConnectionFactory = new TestClientConnectionFactory();

            var connection = CreateServiceConnection(clientConnectionFactory: clientConnectionFactory, handler: new TestConnectionHandler(3000, "foobar"));
            _ = connection.StartAsync();
            _ = connection.WriteFromServiceAsync(new HandshakeResponseMessage());

            var openConnectionMessage = new OpenConnectionMessage("foo", Array.Empty<Claim>());
            _ = connection.WriteFromServiceAsync(openConnectionMessage);
            await connection.ConnectedTask;

            Assert.Equal(1, clientConnectionFactory.Connections.Count);
            var clientConnection = clientConnectionFactory.Connections[0];

            // write a signalr handshake response
            var message = new SignalRProtocol.HandshakeResponseMessage("");
            SignalRProtocol.HandshakeProtocol.WriteResponseMessage(message, clientConnection.Transport.Output);

            // write close connection message
            await connection.WriteFromServiceAsync(new CloseConnectionMessage(clientConnection.ConnectionId));

            // wait until app task completed.
            // await Assert.ThrowsAsync<TimeoutException>(async () => await clientConnection.LifetimeTask.OrTimeout(1000));
            await clientConnection.LifetimeTask;

            using (var writer = new MemoryBufferWriter())
            {
                SignalRProtocol.HandshakeProtocol.WriteResponseMessage(SignalRProtocol.HandshakeResponseMessage.Empty, writer);
                writer.Write(Encoding.UTF8.GetBytes("foobar"));
                var dataMessage = new ConnectionDataMessage(clientConnection.ConnectionId, writer.ToArray());
                await connection.ExpectConnectionDataMessage(dataMessage);
            }

            // expect a signalr close message.
            using (var writer = new MemoryBufferWriter())
            {
                var protocol = clientConnectionFactory.HubProtocol;
                protocol.WriteMessage(SignalRProtocol.CloseMessage.Empty, writer);
                var dataMessage = new ConnectionDataMessage(clientConnection.ConnectionId, writer.ToArray());
                await connection.ExpectConnectionDataMessage(dataMessage);
            }

            await connection.StopAsync();
        }

        private static TestServiceConnection CreateServiceConnection(ConnectionHandler handler = null,
                                                                     TestClientConnectionManager clientConnectionManager = null,
                                                                     string serverId = null,
                                                                     string connectionId = null,
                                                                     GracefulShutdownMode? mode = null,
                                                                     IServiceMessageHandler messageHandler = null,
                                                                     IServiceEventHandler eventHandler = null,
                                                                     IClientConnectionFactory clientConnectionFactory = null,
                                                                     ILoggerFactory loggerFactory = null)
        {
            clientConnectionManager ??= new TestClientConnectionManager();
            clientConnectionFactory ??= new ClientConnectionFactory();

            var container = new TestConnectionContainer();
            var connectionFactory = new TestConnectionFactory(conn =>
            {
                container.Instance = conn;
                return Task.CompletedTask;
            });

            var services = new ServiceCollection();
            var builder = new ConnectionBuilder(services.BuildServiceProvider());

            if (handler == null)
            {
                handler = new TestConnectionHandler();
            }

            return new TestServiceConnection(
                container,
                new ServiceProtocol(),
                clientConnectionManager,
                connectionFactory,
                loggerFactory ?? NullLoggerFactory.Instance,
                handler.OnConnectedAsync,
                clientConnectionFactory,
                serverId ?? "serverId",
                connectionId ?? Guid.NewGuid().ToString("N"),
                null,
                messageHandler ?? new TestServiceMessageHandler(),
                eventHandler ?? new TestServiceEventHandler(),
                mode: mode ?? GracefulShutdownMode.Off
            );
        }

        private sealed class TestConnectionContainer
        {
            public TestConnection Instance { get; set; }
        }

        private sealed class TestConnectionHandler : ConnectionHandler
        {
            private readonly int _shutdownAfter = 0;
            private readonly string _lastWords;

            public TestConnectionHandler(int shutdownAfter = 0, string lastWords = null)
            {
                _shutdownAfter = shutdownAfter;
                _lastWords = lastWords;
            }

            public override async Task OnConnectedAsync(ConnectionContext connection)
            {
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

                // wait application task
                if (_shutdownAfter > 0)
                {
                    await Task.Delay(_shutdownAfter);
                }

                // write last words
                if (!string.IsNullOrEmpty(_lastWords))
                {
                    await connection.Transport.Output.WriteAsync(Encoding.UTF8.GetBytes(_lastWords));
                    await connection.Transport.Output.FlushAsync();
                }

                // write signalr close message
                var protocol = new SignalRProtocol.JsonHubProtocol();
                protocol.WriteMessage(SignalRProtocol.CloseMessage.Empty, connection.Transport.Output);
                await connection.Transport.Output.FlushAsync();
            }
        }

        ///<summary>
        ///   ------------------------- Client Connection------------------------------                 -------------Service Connection---------
        ///  |                                      Transport           Application   |                 |    Transport           Application   |
        ///  | ========================            ============         ===========   |                 |   ============         ===========   |
        ///  | |                      |            |  Input   |         |  Output |   |                 |   |  Input   |         |  Output |   |
        ///  | |      User's          |  /-------  |    |---------------------|   |   |    /-------     |   |    |---------------------|   |   |
        ///  | |      Delegated       |  \-------  |    |---------------------|   |   |    \-------     |   |    |---------------------|   |   |
        ///  | |      Handler         |            |          |         |         |   |                 |   |          |         |         |   |
        ///  | |                      |            |          |         |         |   |                 |   |          |         |         |   |
        ///  | |                      |  -------\  |    |---------------------|   |   |    -------\     |   |    |---------------------|   |   |
        ///  | |                      |  -------/  |    |---------------------|   |   |    -------/     |   |    |---------------------|   |   |
        ///  | |                      |            |  Output  |         |  Input  |   |                 |   |  Output  |         |  Input  |   |
        ///  | ========================            ============        ============   |                 |   ===========          ===========   |
        ///   -------------------------------------------------------------------------                 ----------------------------------------
        /// </summary>
        private sealed class TestServiceConnection : ServiceConnection
        {
            private readonly TestConnectionContainer _container;

            private readonly TaskCompletionSource _connectedTcs = new TaskCompletionSource();
            private readonly TaskCompletionSource _disconnectedTcs = new TaskCompletionSource();

            public TestClientConnectionManager ClientConnectionManager { get; }

            public PipeReader Reader => _connection.Application.Input;
            public PipeWriter Writer => _connection.Application.Output;

            public Task ConnectedTask => _connectedTcs.Task;
            public Task DisconnectedTask => _disconnectedTcs.Task;

            private TestConnection _connection
            {
                get
                {
                    return _container.Instance == null ? throw new Exception("connection needs to be started") : _container.Instance;
                }
            }

            private ServiceProtocol DefaultProtocol { get; } = new ServiceProtocol();

            public TestServiceConnection(TestConnectionContainer container,
                                         IServiceProtocol serviceProtocol,
                                         TestClientConnectionManager clientConnectionManager,
                                         IConnectionFactory connectionFactory,
                                         ILoggerFactory loggerFactory,
                                         ConnectionDelegate connectionDelegate,
                                         IClientConnectionFactory clientConnectionFactory,
                                         string serverId,
                                         string connectionId,
                                         HubServiceEndpoint endpoint,
                                         IServiceMessageHandler serviceMessageHandler,
                                         IServiceEventHandler serviceEventHandler,
                                         ServiceConnectionType connectionType = ServiceConnectionType.Default,
                                         GracefulShutdownMode mode = GracefulShutdownMode.Off,
                                         int closeTimeOutMilliseconds = 10000) : base(
                    serviceProtocol,
                    clientConnectionManager,
                    connectionFactory,
                    loggerFactory,
                    connectionDelegate,
                    clientConnectionFactory,
                    serverId,
                    connectionId,
                    endpoint,
                    serviceMessageHandler,
                    serviceEventHandler,
                    connectionType: connectionType,
                    mode: mode,
                    closeTimeOutMilliseconds: closeTimeOutMilliseconds)
            {
                _container = container;
                ClientConnectionManager = clientConnectionManager;
            }

            private ReadOnlySequence<byte> _payload = new ReadOnlySequence<byte>();

            public async Task ExpectConnectionDataMessage(ConnectionDataMessage expected, CancellationToken token = default)
            {
                ReadOnlySequence<byte> payload;
                if (_payload.IsEmpty)
                {
                    var result = await Reader.ReadAsync(token);
                    var buffer = result.Buffer;
                    Assert.True(ServiceProtocol.TryParseMessage(ref buffer, out var message));
                    Assert.IsType<ConnectionDataMessage>(message);
                    var dataMessage = (ConnectionDataMessage)message;
                    Assert.Equal(expected.ConnectionId, dataMessage.ConnectionId);
                    payload = dataMessage.Payload;
                    Reader.AdvanceTo(buffer.Start);
                }
                else
                {
                    payload = _payload;
                }

                Assert.True(payload.Length >= expected.Payload.Length);
                var actual = payload.Slice(0, expected.Payload.Length);
                Assert.Equal(Encoding.UTF8.GetString(expected.Payload), Encoding.UTF8.GetString(actual));

                _payload = payload.Slice(expected.Payload.Length);
            }

            public void CompleteWriteFromService()
            {
                _connection.Application.Output.Complete();
            }

            public async Task WriteFromServiceAsync(ServiceMessage message)
            {
                await Writer.WriteAsync(DefaultProtocol.GetMessageBytes(message));
                await Writer.FlushAsync();
            }

            protected override ValueTask TrySendPingAsync()
            {
                return ValueTask.CompletedTask;
            }

            protected override async Task OnClientConnectedAsync(OpenConnectionMessage message)
            {
                await base.OnClientConnectedAsync(message);
                _connectedTcs.TrySetResult();
            }

            protected override async Task OnClientDisconnectedAsync(CloseConnectionMessage message)
            {
                await base.OnClientDisconnectedAsync(message);
                _disconnectedTcs.TrySetResult();
            }
        }

        private sealed class TestServiceEventHandler : IServiceEventHandler
        {
            public Task HandleAsync(string connectionId, ServiceEventMessage message)
            {
                return Task.CompletedTask;
            }
        }
    }
}
