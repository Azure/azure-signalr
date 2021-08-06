// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
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
            await connection.ClientConnectionStartedTask;

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

            var connection = CreateServiceConnection(clientConnectionFactory: clientConnectionFactory, handler: new TestConnectionHandler(2000, "foobar"));
            _ = connection.StartAsync();
            _ = connection.WriteFromServiceAsync(new HandshakeResponseMessage());

            var openConnectionMessage = new OpenConnectionMessage("foo", Array.Empty<Claim>());
            _ = connection.WriteFromServiceAsync(openConnectionMessage);
            await connection.ClientConnectionStartedTask;

            Assert.Equal(1, clientConnectionFactory.Connections.Count);
            var clientConnection = clientConnectionFactory.Connections[0];
            Assert.False(clientConnection.IsMigrated);

            // write a signalr handshake response
            var message = new SignalRProtocol.HandshakeResponseMessage("");
            SignalRProtocol.HandshakeProtocol.WriteResponseMessage(message, clientConnection.Transport.Output);
            await clientConnection.Transport.Output.FlushAsync();

            // write a close connection message
            var closeMessage = new CloseConnectionMessage(clientConnection.ConnectionId);
            closeMessage.Headers.Add(Constants.AsrsMigrateTo, "another-server");
            await connection.WriteFromServiceAsync(closeMessage);

            // write a signalr close message
            var protocol = new SignalRProtocol.JsonHubProtocol();
            protocol.WriteMessage(SignalRProtocol.CloseMessage.Empty, clientConnection.Transport.Output);
            await clientConnection.Transport.Output.FlushAsync();

            // expect a handshake response message.
            await connection.ExpectSignalRMessageAsync(SignalRProtocol.HandshakeResponseMessage.Empty);

            // wait until app task completed.
            await Assert.ThrowsAsync<TimeoutException>(async () => await clientConnection.LifetimeTask.OrTimeout(500));
            await clientConnection.LifetimeTask;

            var feature = clientConnection.Features.Get<IConnectionMigrationFeature>();
            Assert.NotNull(feature);
            Assert.Equal("another-server", feature.MigrateTo);

            // signalr close message should be skipped.
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await connection.ExpectSignalRMessageAsync(SignalRProtocol.CloseMessage.Empty, protocol, cts.Token));

            await connection.StopAsync();
        }

        [Fact]
        public async Task TestCloseConnectionMessage()
        {
            var clientConnectionFactory = new TestClientConnectionFactory();

            var connection = CreateServiceConnection(clientConnectionFactory: clientConnectionFactory, handler: new TestConnectionHandler(2000, "foobar"));
            _ = connection.StartAsync();
            _ = connection.WriteFromServiceAsync(new HandshakeResponseMessage());

            var openConnectionMessage = new OpenConnectionMessage("foo", Array.Empty<Claim>());
            _ = connection.WriteFromServiceAsync(openConnectionMessage);
            await connection.ClientConnectionStartedTask;

            Assert.Equal(1, clientConnectionFactory.Connections.Count);
            var clientConnection = clientConnectionFactory.Connections[0];

            // write a signalr handshake response
            var message = new SignalRProtocol.HandshakeResponseMessage("");
            SignalRProtocol.HandshakeProtocol.WriteResponseMessage(message, clientConnection.Transport.Output);

            // write a close connection message
            var closeMessage = new CloseConnectionMessage(clientConnection.ConnectionId);
            await connection.WriteFromServiceAsync(closeMessage);

            // write a signalr close message
            var protocol = new SignalRProtocol.JsonHubProtocol();
            protocol.WriteMessage(SignalRProtocol.CloseMessage.Empty, clientConnection.Transport.Output);
            await clientConnection.Transport.Output.FlushAsync();

            // expect a signalr handshake response and a signalr close message.
            await connection.ExpectSignalRMessageAsync(SignalRProtocol.HandshakeResponseMessage.Empty);
            await connection.ExpectSignalRMessageAsync(SignalRProtocol.CloseMessage.Empty, protocol);

            // wait until app task completed.
            await Assert.ThrowsAsync<TimeoutException>(async () => await clientConnection.LifetimeTask.OrTimeout(500));
            await clientConnection.LifetimeTask;

            // expect a last words.
            await connection.ExpectServiceMessageAsync(new ConnectionDataMessage("foo", Encoding.UTF8.GetBytes("foobar")));

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

            private TaskCompletionSource<object> _startedTcs = new TaskCompletionSource<object>();

            public Task Started => _startedTcs.Task;

            public TestConnectionHandler(int shutdownAfter = 0, string lastWords = null)
            {
                _shutdownAfter = shutdownAfter;
                _lastWords = lastWords;
            }

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
                            if (_shutdownAfter > 0)
                            {
                                await Task.Delay(_shutdownAfter);
                            }
                            if (!string.IsNullOrEmpty(_lastWords))
                            {
                                await connection.Transport.Output.WriteAsync(Encoding.UTF8.GetBytes(_lastWords));
                                await connection.Transport.Output.FlushAsync();
                            }
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

            public TestClientConnectionManager ClientConnectionManager { get; }

            public PipeReader Reader => _connection.Application.Input;
            public PipeWriter Writer => _connection.Application.Output;

            public Task ClientConnectionStartedTask => _connectedTcs.Task;

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

            public async Task ExpectServiceMessageAsync<T>(T expected, CancellationToken token = default) where T : ServiceMessage
            {
                var result = await Reader.ReadAsync(token);
                var buffer = result.Buffer;

                Assert.True(ServiceProtocol.TryParseMessage(ref buffer, out var actual));
                Assert.IsType<T>(actual);

                if (expected is ConnectionDataMessage p)
                {
                    var q = (ConnectionDataMessage)actual;
                    Assert.Equal(p.ConnectionId, q.ConnectionId);
                    Assert.Equal(Encoding.UTF8.GetString(p.Payload), Encoding.UTF8.GetString(q.Payload));
                }
            }

            public async Task ExpectSignalRMessageAsync<T>(T expected, SignalRProtocol.IHubProtocol hubProtocol = null, CancellationToken token = default) where T : SignalRProtocol.HubMessage
            {
                var result = await Reader.ReadAsync(token);

                var buffer = result.Buffer;

                Assert.True(ServiceProtocol.TryParseMessage(ref buffer, out var message));
                Assert.IsType<ConnectionDataMessage>(message);
                var dataMessage = (ConnectionDataMessage)message;
                var payload = dataMessage.Payload;

                if (expected is SignalRProtocol.HandshakeRequestMessage req)
                {
                    Assert.True(SignalRProtocol.HandshakeProtocol.TryParseRequestMessage(ref payload, out var actual));
                    Assert.Equal(req, actual);
                }
                else if (expected is SignalRProtocol.HandshakeResponseMessage)
                {
                    Assert.True(SignalRProtocol.HandshakeProtocol.TryParseResponseMessage(ref payload, out var actual));
                    Assert.IsType<T>(actual);
                }
                else
                {
                    Assert.NotNull(hubProtocol);
                    Assert.True(hubProtocol.TryParseMessage(ref payload, null, out var actual));
                    Assert.IsType<T>(actual);
                }
                Reader.AdvanceTo(buffer.Start);

                if (!payload.IsEmpty)
                {
                    var data = new ConnectionDataMessage(dataMessage.ConnectionId, payload.Slice(payload.Start));
                    ServiceProtocol.WriteMessage(data, _connection.Transport.Output);
                    await _connection.Transport.Output.FlushAsync();
                }
            }

            public void CompleteWriteFromService()
            {
                _connection.Application.Output.Complete();
            }

            public async Task WriteFromServiceAsync(ServiceMessage message)
            {
                await Writer.WriteAsync(DefaultProtocol.GetMessageBytes(message));
            }

            protected override async Task OnClientConnectedAsync(OpenConnectionMessage message)
            {
                await base.OnClientConnectedAsync(message);
                _connectedTcs.TrySetResult();
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
