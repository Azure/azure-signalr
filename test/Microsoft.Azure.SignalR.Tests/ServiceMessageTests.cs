// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Security.Claims;
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
using static Microsoft.Azure.SignalR.Tests.ServiceConnectionTests;

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
        public async Task TestServiceConnectionForMigratedIn()
        {
            var clientConnectionFactory = new TestClientConnectionFactory();

            var connection = CreateServiceConnection(clientConnectionFactory: clientConnectionFactory);

            // create a connection with migration header.
            await connection.OnClientConnectedAsyncForTest(new OpenConnectionMessage("foo", new Claim[0])
            {
                Headers = new Dictionary<string, StringValues>{
                    { Constants.AsrsMigrateFrom, "another-server" }
                }
            });

            Assert.Equal(1, clientConnectionFactory.Connections.Count);
            var context = clientConnectionFactory.Connections[0];
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

            CancellationTokenSource cts = new();
            var connection = CreateServiceConnection(clientConnectionFactory: factory, mode: GracefulShutdownMode.MigrateClients);

            // create a connection with migration header.
            await connection.OnClientConnectedAsyncForTest(new OpenConnectionMessage("foo", new Claim[0]));

            var context = factory.Connections[0];

            var closeMessage = new CloseConnectionMessage(context.ConnectionId);
            closeMessage.Headers.Add(Constants.AsrsMigrateTo, "another-server");

            var disconnect = connection.OnClientDisconnectedAsyncForTest(closeMessage);

            var feature = context.Features.Get<IConnectionMigrationFeature>();
            Assert.NotNull(feature);
            Assert.Equal("another-server", feature.MigrateTo);

            cts.Cancel();
            await disconnect.OrTimeout();
        }

        private static TestServiceConnection CreateServiceConnection(Action<ConnectionBuilder> use = null,
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

            if (use == null)
            {
                use = (builder) => builder.UseConnectionHandler<TestConnectionHandler>();
            }
            use(builder);

            builder.UseConnectionHandler<TestConnectionHandler>();

            ConnectionDelegate handler = builder.Build();

            return new TestServiceConnection(
                container,
                new ServiceProtocol(),
                clientConnectionManager,
                connectionFactory,
                loggerFactory ?? NullLoggerFactory.Instance,
                handler,
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

        ///<summary>
        ///   ------------------------- Client Connection-------------------------------                   ------------Service Connection---------
        ///  |                                      Transport              Application  |                 |   Transport              Application  |
        ///  | ========================            =============         ============   |                 |  =============         ============   |
        ///  | |                      |            |   Input   |         |   Output |   |                 |  |   Input   |         |   Output |   |
        ///  | |      User's          |  /-------  |     |---------------------|    |   |    /-------     |  |     |---------------------|    |   |
        ///  | |      Delegated       |  \-------  |     |---------------------|    |   |    \-------     |  |     |---------------------|    |   |
        ///  | |      Handler         |            |           |         |          |   |                 |  |           |         |          |   |
        ///  | |                      |            |           |         |          |   |                 |  |           |         |          |   |
        ///  | |                      |  -------\  |     |---------------------|    |   |    -------\     |  |     |---------------------|    |   |
        ///  | |                      |  -------/  |     |---------------------|    |   |    -------/     |  |     |---------------------|    |   |
        ///  | |                      |            |   Output  |         |   Input  |   |                 |  |   Output  |         |   Input  |   |
        ///  | ========================            ============         ============    |                 |  ============         ============    |
        ///   --------------------------------------------------------------------------                   ---------------------------------------
        /// </summary>
        private sealed class TestServiceConnection : ServiceConnection
        {
            private readonly TestConnectionContainer _container;

            public TestClientConnectionManager ClientConnectionManager { get; }

            public PipeReader Reader => _connection.Transport.Input;
            public PipeWriter Writer => _connection.Application.Output;

            private TestConnection _connection
            {
                get
                {
                    return _container.Instance == null ? throw new System.Exception("connection needs to be started") : _container.Instance;
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

            public void CompleteWriteFromService()
            {
                _connection.Application.Output.Complete();
            }

            public Task OnClientConnectedAsyncForTest(OpenConnectionMessage message)
            {
                return OnClientConnectedAsync(message);
            }

            public Task OnClientDisconnectedAsyncForTest(CloseConnectionMessage message)
            {
                return OnClientDisconnectedAsync(message);
            }

            public async Task WriteFromServiceAsync(ServiceMessage message)
            {
                await Writer.WriteAsync(DefaultProtocol.GetMessageBytes(message));
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
