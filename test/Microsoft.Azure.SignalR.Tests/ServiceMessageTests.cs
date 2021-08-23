﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Azure.Identity;

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
        private const string _signingKey = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        private const string _aadConnectionString = "endpoint=https://localhost;authType=aad;";

        private const string _keyConnectionString = "endpoint=https://localhost;accessKey=" + _signingKey;

        public ServiceMessageTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task TestOpenConnectionMessageWithMigrateIn()
        {
            var clientConnectionFactory = new TestClientConnectionFactory();
            var connection = CreateServiceConnection(clientConnectionFactory: clientConnectionFactory);
            _ = connection.StartAsync();
            await connection.ConnectionInitializedTask.OrTimeout(1000);

            var openConnectionMessage = new OpenConnectionMessage("foo", Array.Empty<Claim>());
            openConnectionMessage.Headers.Add(Constants.AsrsMigrateFrom, "another-server");
            _ = connection.WriteFromServiceAsync(openConnectionMessage);
            await connection.ClientConnectedTask;

            Assert.Equal(1, clientConnectionFactory.Connections.Count);
            var clientConnection = clientConnectionFactory.Connections[0];
            Assert.True(clientConnection.IsMigrated);

            // write a handshake response
            var message = new SignalRProtocol.HandshakeResponseMessage("");
            SignalRProtocol.HandshakeProtocol.WriteResponseMessage(message, clientConnection.Transport.Output);
            await clientConnection.Transport.Output.FlushAsync();

            // signalr handshake response should be skipped.
            await Assert.ThrowsAsync<TimeoutException>(async () => await connection.ExpectSignalRMessage(SignalRProtocol.HandshakeResponseMessage.Empty).OrTimeout(1000));

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
            await connection.ConnectionInitializedTask.OrTimeout(1000);

            var openConnectionMessage = new OpenConnectionMessage("foo", Array.Empty<Claim>());
            _ = connection.WriteFromServiceAsync(openConnectionMessage);
            await connection.ClientConnectedTask;

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
            await Assert.ThrowsAsync<TimeoutException>(async () => await clientConnection.LifetimeTask.OrTimeout(1000));
            await clientConnection.LifetimeTask.OrTimeout(3000);

            // expect a handshake response message.
            await connection.ExpectSignalRMessage(SignalRProtocol.HandshakeResponseMessage.Empty).OrTimeout(3000);

            // signalr close message should be skipped.
            await Assert.ThrowsAsync<TimeoutException>(async () => await connection.ExpectSignalRMessage(SignalRProtocol.CloseMessage.Empty).OrTimeout(1000));

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
            await connection.ConnectionInitializedTask.OrTimeout(1000);

            var openConnectionMessage = new OpenConnectionMessage("foo", Array.Empty<Claim>());
            _ = connection.WriteFromServiceAsync(openConnectionMessage);
            await connection.ClientConnectedTask;

            Assert.Equal(1, clientConnectionFactory.Connections.Count);
            var clientConnection = clientConnectionFactory.Connections[0];

            // write a signalr handshake response
            var message = new SignalRProtocol.HandshakeResponseMessage("");
            SignalRProtocol.HandshakeProtocol.WriteResponseMessage(message, clientConnection.Transport.Output);

            // write close connection message
            await connection.WriteFromServiceAsync(new CloseConnectionMessage(clientConnection.ConnectionId));

            // wait until app task completed.
            await Assert.ThrowsAsync<TimeoutException>(async () => await clientConnection.LifetimeTask.OrTimeout(1000));
            await clientConnection.LifetimeTask;

            await connection.ExpectSignalRMessage(SignalRProtocol.HandshakeResponseMessage.Empty).OrTimeout(1000);
            await connection.ExpectStringMessage("foobar").OrTimeout(1000);
            await connection.ExpectSignalRMessage(SignalRProtocol.CloseMessage.Empty).OrTimeout(1000);

            await connection.StopAsync();
        }

        [Theory]
        [InlineData(nameof(AccessKey))]
        [InlineData(nameof(AadAccessKey))]
        public async Task TestAccessKeyRequestMessage(string keyType)
        {
            var endpoint = keyType switch
            {
                nameof(AccessKey) => new ServiceEndpoint(_keyConnectionString),
                nameof(AadAccessKey) => new ServiceEndpoint(_aadConnectionString),
                _ => throw new NotImplementedException()
            };
            var hubServiceEndpoint = new HubServiceEndpoint("foo", null, endpoint);

            var connection = CreateServiceConnection(hubServiceEndpoint: hubServiceEndpoint);
            _ = connection.StartAsync();
            await connection.ConnectionInitializedTask.OrTimeout(1000);

            if (endpoint.AccessKey is TestAadAccessKey aadKey)
            {
                var message = await connection.ExpectServiceMessage<AccessKeyRequestMessage>().OrTimeout(3000);
                Assert.Equal(aadKey.Token, message.Token);
            }
            else
            {
                await AssertTimeoutAsync(connection.ExpectServiceMessage<AccessKeyRequestMessage>());
            }
        }

        [Theory]
        [InlineData(typeof(AccessKey), _keyConnectionString)]
        [InlineData(typeof(AadAccessKey), _aadConnectionString)]
        public async Task TestAccessKeyResponseMessage(Type type, string connectionString)
        {
            var endpoint = new ServiceEndpoint(connectionString);
            Assert.Equal(type.Name, endpoint.AccessKey.GetType().Name);
            var hubServiceEndpoint = new HubServiceEndpoint("foo", null, endpoint);

            var connection = CreateServiceConnection(hubServiceEndpoint: hubServiceEndpoint);
            _ = connection.StartAsync();
            await connection.ConnectionInitializedTask.OrTimeout(1000);

            var message = new AccessKeyResponseMessage()
            {
                Kid = "foo",
                AccessKey = _signingKey
            };
            await connection.WriteFromServiceAsync(message);

            var audience = "http://localhost/chat";
            var claims = Array.Empty<Claim>();
            var lifetime = TimeSpan.FromHours(1);
            var algorithm = AccessTokenAlgorithm.HS256;

            var clientToken = await endpoint.AccessKey.GenerateAccessTokenAsync(audience, claims, lifetime, algorithm).OrTimeout(TimeSpan.FromSeconds(3));
            Assert.NotNull(clientToken);

            await connection.StopAsync();
        }

        [Fact]
        public async Task TestAccessKeyResponseMessageWithError()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Error, expectedErrors: c => true,
                logChecker: logs =>
                {
                    Assert.Equal(1, logs.Count);
                    return true;
                }))
            {
                var connection = CreateServiceConnection(loggerFactory: loggerFactory);
                var connectionTask = connection.StartAsync();
                await connection.ConnectionInitializedTask.OrTimeout(1000);

                Assert.Equal(ServiceConnectionStatus.Connected, connection.Status);

                var message = new AccessKeyResponseMessage()
                {
                    ErrorType = nameof(ArgumentException),
                    ErrorMessage = "This is a error messsage"
                };
                await connection.WriteFromServiceAsync(message);

                // complete reading to end the connection
                connection.CompleteWriteFromService();

                await connectionTask.OrTimeout();
                Assert.Equal(ServiceConnectionStatus.Disconnected, connection.Status);

                Assert.Empty(connection.ClientConnectionManager.ClientConnections);
            }
        }

        private static async Task AssertTimeoutAsync(Task task, int milliseconds = 3000)
        {
            await Assert.ThrowsAsync<TimeoutException>(async () => await task.OrTimeout(milliseconds));
        }

        private static TestServiceConnection CreateServiceConnection(ConnectionHandler handler = null,
                                                                     TestClientConnectionManager clientConnectionManager = null,
                                                                     string serverId = null,
                                                                     string connectionId = null,
                                                                     GracefulShutdownMode? mode = null,
                                                                     IServiceMessageHandler messageHandler = null,
                                                                     IServiceEventHandler eventHandler = null,
                                                                     IClientConnectionFactory clientConnectionFactory = null,
                                                                     HubServiceEndpoint hubServiceEndpoint = null,
                                                                     ILoggerFactory loggerFactory = null)
        {
            clientConnectionManager ??= new TestClientConnectionManager();
            clientConnectionFactory ??= new TestClientConnectionFactory();

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
                hubServiceEndpoint ?? new TestHubServiceEndpoint(),
                messageHandler ?? new TestServiceMessageHandler(),
                eventHandler ?? new TestServiceEventHandler(),
                mode: mode ?? GracefulShutdownMode.Off
            );
        }

        private class TestAadAccessKey : AadAccessKey
        {
            public string Token { get; } = Guid.NewGuid().ToString();

            public TestAadAccessKey() : base("http://localhost:80", new DefaultAzureCredential())
            {
            }

            public override Task<string> GenerateAadTokenAsync(CancellationToken ctoken = default)
            {
                return Task.FromResult(Token);
            }
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

            private readonly TaskCompletionSource _clientConnectedTcs = new TaskCompletionSource();
            private readonly TaskCompletionSource _clientDisconnectedTcs = new TaskCompletionSource();

            private ReadOnlySequence<byte> _payload = new ReadOnlySequence<byte>();
            public TestClientConnectionManager ClientConnectionManager { get; }

            public PipeReader Reader => _connection.Application.Input;
            public PipeWriter Writer => _connection.Application.Output;

            public Task ClientConnectedTask => _clientConnectedTcs.Task;
            public Task ClientDisconnectedTask => _clientDisconnectedTcs.Task;

            public ServiceProtocol DefaultServiceProtocol { get; } = new ServiceProtocol();

            public SignalRProtocol.IHubProtocol DefaultHubProtocol { get; } = new SignalRProtocol.JsonHubProtocol();

            private TestConnection _connection
            {
                get
                {
                    return _container.Instance == null ? throw new Exception("connection needs to be started") : _container.Instance;
                }
            }

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
            public async Task ExpectStringMessage(string expected, string connectionId = null)
            {
                var payload = await GetPayloadAsync(connectionId: connectionId);
                var expectedBytes = Encoding.UTF8.GetBytes(expected);

                Assert.True(payload.Length >= expectedBytes.Length);
                var actualBytes = payload.Slice(0, expectedBytes.Length);
                Assert.Equal(expected, Encoding.UTF8.GetString(actualBytes));

                _payload = payload.Slice(expectedBytes.Length);
            }

            public async Task<T> ExpectServiceMessage<T>() where T: ServiceMessage
            {
                var result = await Reader.ReadAsync();
                var buffer = result.Buffer;
                Assert.True(ServiceProtocol.TryParseMessage(ref buffer, out var actual));
                Assert.IsType<T>(actual);
                Reader.AdvanceTo(buffer.Start);
                return (T)actual;
            }

            public async Task ExpectSignalRMessage<T>(T message, string connectionId = null)
            {
                var payload = await GetPayloadAsync(connectionId: connectionId);

                if (message is SignalRProtocol.HandshakeRequestMessage)
                {
                    Assert.True(SignalRProtocol.HandshakeProtocol.TryParseRequestMessage(ref payload, out _));
                }
                else if (message is SignalRProtocol.HandshakeResponseMessage)
                {
                    Assert.True(SignalRProtocol.HandshakeProtocol.TryParseResponseMessage(ref payload, out _));
                }
                else
                {
                    Assert.True(DefaultHubProtocol.TryParseMessage(ref payload, null, out var actual));
                    Assert.IsType<T>(actual);
                }
                _payload = payload;
            }

            public void CompleteWriteFromService()
            {
                _connection.Application.Output.Complete();
            }

            public async Task WriteFromServiceAsync(ServiceMessage message)
            {
                await Writer.WriteAsync(DefaultServiceProtocol.GetMessageBytes(message));
                await Writer.FlushAsync();
            }

            protected override ValueTask TrySendPingAsync()
            {
                return ValueTask.CompletedTask;
            }

            protected override async Task OnClientConnectedAsync(OpenConnectionMessage message)
            {
                await base.OnClientConnectedAsync(message);
                _clientConnectedTcs.TrySetResult();
            }

            protected override async Task OnClientDisconnectedAsync(CloseConnectionMessage message)
            {
                await base.OnClientDisconnectedAsync(message);
                _clientDisconnectedTcs.TrySetResult();
            }

            private async Task<ReadOnlySequence<byte>> GetPayloadAsync(string connectionId = null)
            {
                if (_payload.IsEmpty)
                {
                    var result = await Reader.ReadAsync();
                    var buffer = result.Buffer;

                    Assert.True(ServiceProtocol.TryParseMessage(ref buffer, out var message));
                    Assert.IsType<ConnectionDataMessage>(message);
                    var dataMessage = (ConnectionDataMessage)message;

                    if (!string.IsNullOrEmpty(connectionId))
                    {
                        Assert.Equal(connectionId, dataMessage.ConnectionId);
                    }
                    Reader.AdvanceTo(buffer.Start);
                    return dataMessage.Payload;
                }
                else
                {
                    return _payload;
                }
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
