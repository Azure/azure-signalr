// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

using SignalRProtocol = Microsoft.AspNetCore.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.Tests;

public class ServiceMessageTests : VerifiableLoggedTest
{
    private const string SigningKey = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private const string MicrosoftEntraConnectionString = "endpoint=https://localhost;authType=aad;";

    private const string LocalConnectionString = "endpoint=https://localhost;accessKey=" + SigningKey;

    public ServiceMessageTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task TestOpenConnectionMessageWithMigrateIn()
    {
        var isMigratedProperty = typeof(ClientConnectionContext).GetField("_isMigrated", BindingFlags.Instance | BindingFlags.NonPublic);

        var clientConnectionFactory = new TestClientConnectionFactory();
        var clientInvocationManager = new DefaultClientInvocationManager();
        var connection = CreateServiceConnection(clientConnectionFactory: clientConnectionFactory, clientInvocationManager: clientInvocationManager);
        _ = connection.StartAsync();
        await connection.ConnectionInitializedTask.OrTimeout();

        var openConnectionMessage = new OpenConnectionMessage("foo", Array.Empty<Claim>()) { Protocol = "json" };
        openConnectionMessage.Headers.Add(Constants.AsrsMigrateFrom, "another-server");
        _ = connection.WriteFromServiceAsync(openConnectionMessage);
        await connection.ClientConnectedTask.OrTimeout();

        Assert.Equal(1, clientConnectionFactory.Connections.Count);
        var clientConnection = clientConnectionFactory.Connections[0];
        Assert.True((bool)isMigratedProperty.GetValue(clientConnection));

        // write a handshake response
        var message = new SignalRProtocol.HandshakeResponseMessage("");
        SignalRProtocol.HandshakeProtocol.WriteResponseMessage(message, clientConnection.Transport.Output);
        await clientConnection.Transport.Output.FlushAsync();

        // signalr handshake response should be skipped.
        await Assert.ThrowsAsync<TimeoutException>(async () => await connection.ExpectSignalRMessage(SignalRProtocol.HandshakeResponseMessage.Empty).OrTimeout(1000));

        Assert.True((bool)isMigratedProperty.GetValue(clientConnection));

        var feature = clientConnection.Features.Get<IConnectionMigrationFeature>();
        Assert.NotNull(feature);
        Assert.Equal("another-server", feature.MigrateFrom);

        await connection.StopAsync();
    }

    [Fact]
    public async Task TestCloseConnectionMessageWithMigrateOut()
    {
        var isMigratedProperty = typeof(ClientConnectionContext).GetField("_isMigrated", BindingFlags.Instance | BindingFlags.NonPublic);

        var clientConnectionFactory = new TestClientConnectionFactory();
        var clientInvocationManager = new DefaultClientInvocationManager();

        var connection = CreateServiceConnection(clientConnectionFactory: clientConnectionFactory, handler: new TestConnectionHandler(3000, "foobar"), clientInvocationManager: clientInvocationManager);
        _ = connection.StartAsync();
        await connection.ConnectionInitializedTask.OrTimeout(1000);

        var openConnectionMessage = new OpenConnectionMessage("foo", Array.Empty<Claim>()) { Protocol = "json" };
        _ = connection.WriteFromServiceAsync(openConnectionMessage);
        await connection.ClientConnectedTask;

        Assert.Equal(1, clientConnectionFactory.Connections.Count);
        var clientConnection = clientConnectionFactory.Connections[0];
        Assert.False((bool)isMigratedProperty.GetValue(clientConnection));

        // write a signalr handshake response
        var message = new SignalRProtocol.HandshakeResponseMessage("");
        SignalRProtocol.HandshakeProtocol.WriteResponseMessage(message, clientConnection.Transport.Output);
        await clientConnection.Transport.Output.FlushAsync();

        // expect a handshake response message.
        await connection.ExpectSignalRMessage(SignalRProtocol.HandshakeResponseMessage.Empty).OrTimeout();

        // write a close connection message with migration header
        var closeMessage = new CloseConnectionMessage(clientConnection.ConnectionId);
        closeMessage.Headers.Add(Constants.AsrsMigrateTo, "another-server");
        await connection.WriteFromServiceAsync(closeMessage);

        // wait until app task completed.
        await clientConnection.LifetimeTask.OrTimeout();

        var feature = clientConnection.Features.Get<IConnectionMigrationFeature>();
        Assert.NotNull(feature);
        Assert.Equal("another-server", feature.MigrateTo);

        await connection.StopAsync();
    }

    [Fact]
    public async Task TestMigrateInAndNormalClose()
    {
        var clientConnectionFactory = new TestClientConnectionFactory();
        var clientInvocationManager = new DefaultClientInvocationManager();
        var connection = CreateServiceConnection(clientConnectionFactory: clientConnectionFactory, clientInvocationManager: clientInvocationManager);
        _ = connection.StartAsync();
        await connection.ConnectionInitializedTask.OrTimeout();

        var openConnectionMessage = new OpenConnectionMessage("foo", Array.Empty<Claim>()) { Protocol = "json" };
        openConnectionMessage.Headers.Add(Constants.AsrsMigrateFrom, "another-server");
        _ = connection.WriteFromServiceAsync(openConnectionMessage);
        await connection.ClientConnectedTask.OrTimeout();

        Assert.Equal(1, clientConnectionFactory.Connections.Count);
        var clientConnection = clientConnectionFactory.Connections[0];
        var feature = clientConnection.Features.Get<IConnectionMigrationFeature>();
        Assert.NotNull(feature);
        Assert.Equal("another-server", feature.MigrateFrom);

        // write a handshake response
        var message = new SignalRProtocol.HandshakeResponseMessage("");
        SignalRProtocol.HandshakeProtocol.WriteResponseMessage(message, clientConnection.Transport.Output);
        await clientConnection.Transport.Output.FlushAsync();

        // signalr handshake response should be skipped.
        await Assert.ThrowsAsync<TimeoutException>(async () => await connection.ExpectSignalRMessage(SignalRProtocol.HandshakeResponseMessage.Empty).OrTimeout(1000));

        // write close connection message
        await connection.WriteFromServiceAsync(new CloseConnectionMessage(clientConnection.ConnectionId));

        // wait until app task completed.
        await clientConnection.LifetimeTask;

        await connection.StopAsync();

        Assert.Null(clientConnection.Features.Get<IConnectionMigrationFeature>());
    }

    [Fact]
    public async Task TestCloseConnectionMessage()
    {
        var clientConnectionFactory = new TestClientConnectionFactory();
        var clientInvocationManager = new DefaultClientInvocationManager();

        const string lastWill = "{\"type\":1,\"target\":\"test\",\"arguments\":[\"last will\"]}\u001e";

        var connection = CreateServiceConnection(clientConnectionFactory: clientConnectionFactory, handler: new TestConnectionHandler(3000, lastWill), clientInvocationManager: clientInvocationManager);
        _ = connection.StartAsync();
        await connection.ConnectionInitializedTask.OrTimeout(1000);

        var openConnectionMessage = new OpenConnectionMessage("foo", Array.Empty<Claim>()) { Protocol = "json" };
        _ = connection.WriteFromServiceAsync(openConnectionMessage);
        await connection.ClientConnectedTask;

        Assert.Equal(1, clientConnectionFactory.Connections.Count);
        var clientConnection = clientConnectionFactory.Connections[0];

        // write a signalr handshake response
        SignalRProtocol.HandshakeProtocol.WriteResponseMessage(SignalRProtocol.HandshakeResponseMessage.Empty, clientConnection.Transport.Output);

        // write close connection message
        await connection.WriteFromServiceAsync(new CloseConnectionMessage(clientConnection.ConnectionId));

        // wait until app task completed.
        await clientConnection.LifetimeTask;

        await connection.ExpectSignalRMessage(SignalRProtocol.HandshakeResponseMessage.Empty).OrTimeout(1000);
        await connection.ExpectStringMessage(lastWill).OrTimeout(1000);
        await connection.ExpectSignalRMessage(SignalRProtocol.CloseMessage.Empty).OrTimeout(1000);

        await connection.StopAsync();
    }

    [Theory]
    [InlineData(typeof(AccessKey))]
    [InlineData(typeof(MicrosoftEntraAccessKey))]
    public async Task TestAccessKeyRequestMessage(Type keyType)
    {
        var endpoint = MockServiceEndpoint(keyType.Name);
        Assert.IsAssignableFrom(keyType, endpoint.AccessKey);
        var hubServiceEndpoint = new HubServiceEndpoint("foo", null, endpoint);
        var clientInvocationManager = new DefaultClientInvocationManager();

        var connection = CreateServiceConnection(hubServiceEndpoint: hubServiceEndpoint, clientInvocationManager: clientInvocationManager);
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
    [InlineData(typeof(AccessKey))]
    [InlineData(typeof(MicrosoftEntraAccessKey))]
    public async Task TestAccessKeyResponseMessage(Type keyType)
    {
        var endpoint = MockServiceEndpoint(keyType.Name);
        Assert.IsAssignableFrom(keyType, endpoint.AccessKey);
        var hubServiceEndpoint = new HubServiceEndpoint("foo", null, endpoint);
        var clientInvocationManager = new DefaultClientInvocationManager();

        var connection = CreateServiceConnection(hubServiceEndpoint: hubServiceEndpoint, clientInvocationManager: clientInvocationManager);
        _ = connection.StartAsync();
        await connection.ConnectionInitializedTask.OrTimeout(1000);

        var message = new AccessKeyResponseMessage()
        {
            Kid = "foo",
            AccessKey = SigningKey
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

    [Theory]
    [InlineData(5, true)]
    [InlineData(60, true)]
    [InlineData(119, true)]
    [InlineData(121, false)] // becomes unavailable only after the key has expired.
    public async Task TestAccessKeyResponseMessageWithError(int minutesElapsed, bool expectAvailable)
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Error, expectedErrors: c => true))
        {
            var endpoint = new TestHubServiceEndpoint(endpoint: new TestServiceEndpoint(new DefaultAzureCredential()));
            var key = Assert.IsType<MicrosoftEntraAccessKey>(endpoint.AccessKey);
            key.UpdateAccessKey("foo", "bar");

            var field = typeof(MicrosoftEntraAccessKey).GetField("_updateAt", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(key, DateTime.UtcNow - TimeSpan.FromMinutes(minutesElapsed));

            var clientInvocationManager = new DefaultClientInvocationManager();

            var connection = CreateServiceConnection(loggerFactory: loggerFactory, hubServiceEndpoint: endpoint, clientInvocationManager: clientInvocationManager);
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

            Assert.Equal(expectAvailable, key.Available);
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
                                                                 IClientInvocationManager clientInvocationManager = null,
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

        handler ??= new TestConnectionHandler();

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
            clientInvocationManager,
            new DefaultHubProtocolResolver(new[] { new SignalRProtocol.JsonHubProtocol() }, NullLogger<DefaultHubProtocolResolver>.Instance),
            mode: mode ?? GracefulShutdownMode.Off
        );
    }

    private ServiceEndpoint MockServiceEndpoint(string keyTypeName)
    {
        switch (keyTypeName)
        {
            case nameof(AccessKey):
                return new ServiceEndpoint(LocalConnectionString);

            case nameof(MicrosoftEntraAccessKey):
                var endpoint = new ServiceEndpoint(MicrosoftEntraConnectionString);
                var field = typeof(ServiceEndpoint).GetField("_accessKey", BindingFlags.NonPublic | BindingFlags.Instance);
                field.SetValue(endpoint, new TestAadAccessKey());
                return endpoint;

            default:
                throw new NotImplementedException();
        }
    }

    private class TestAadAccessKey : MicrosoftEntraAccessKey
    {
        public string Token { get; } = Guid.NewGuid().ToString();

        public TestAadAccessKey() : base(new Uri("http://localhost:80"), new DefaultAzureCredential())
        {
        }

        public override Task<string> GetMicrosoftEntraTokenAsync(CancellationToken ctoken = default)
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

        public PipeReader Reader => Connection.Application.Input;

        public PipeWriter Writer => Connection.Application.Output;

        public Task ClientConnectedTask => _clientConnectedTcs.Task;

        public Task ClientDisconnectedTask => _clientDisconnectedTcs.Task;

        public ServiceProtocol DefaultServiceProtocol { get; } = new ServiceProtocol();

        public SignalRProtocol.IHubProtocol DefaultHubProtocol { get; } = new SignalRProtocol.JsonHubProtocol();

        private TestConnection Connection => _container.Instance ?? throw new Exception("connection needs to be started");

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
                                     IClientInvocationManager clientInvocationManager,
                                     IHubProtocolResolver hubProtocolResolver,
                                     ServiceConnectionType connectionType = ServiceConnectionType.Default,
                                     GracefulShutdownMode mode = GracefulShutdownMode.Off) : base(
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
                clientInvocationManager,
                hubProtocolResolver,
                connectionType: connectionType,
                mode: mode)
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

        public async Task<T> ExpectServiceMessage<T>() where T : ServiceMessage
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
            Connection.Application.Output.Complete();
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
