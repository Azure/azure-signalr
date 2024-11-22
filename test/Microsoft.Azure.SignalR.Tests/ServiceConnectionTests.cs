// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;

using Xunit;
using Xunit.Abstractions;

using SignalRProtocol = Microsoft.AspNetCore.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.Tests;

public class ServiceConnectionTests : VerifiableLoggedTest
{
    public ServiceConnectionTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task TestServiceConnectionHandleOfflineMessageTask()
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Information, logChecker: logs =>
        {
            return logs.Where(x => x.Write.EventId.Name == "ReceivedConnectionOffline").Single() != null;
        }))
        {
            var ccm = new TestClientConnectionManager();
            var ccf = new ClientConnectionFactory(loggerFactory);
            var protocol = new ServiceProtocol();
            var hubProtocol = new JsonHubProtocol();
            TestConnection transportConnection = null;
            var connectionFactory = new TestConnectionFactory(conn =>
            {
                transportConnection = conn;
                return Task.CompletedTask;
            });
            var services = new ServiceCollection();
            var builder = new ConnectionBuilder(services.BuildServiceProvider());
            builder.UseConnectionHandler<TestConnectionHandler>();
            var handler = builder.Build();

            var serviceConnection = CreateServiceConnection(protocol, hubProtocol, ccm, ccf, connectionFactory, loggerFactory, handler);
            var connectionTask = serviceConnection.StartAsync();

            // completed handshake
            await serviceConnection.ConnectionInitializedTask.OrTimeout();
            Assert.Equal(ServiceConnectionStatus.Connected, serviceConnection.Status);

            // send offline message
            var message = new ConnectionFlowControlMessage(serviceConnection.ConnectionId, ConnectionFlowControlOperation.Offline, ConnectionType.Server);
            await transportConnection.Application.Output.WriteAsync(protocol.GetMessageBytes(message));

            // complete reading to end the connection
            transportConnection.Application.Output.Complete();

            await connectionTask.OrTimeout();
            Assert.Equal(ServiceConnectionStatus.Disconnected, serviceConnection.Status);
        }
    }

    [Fact]
    public async Task TestServiceConnectionHandlePauseMessageTask()
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Information, logChecker: logs =>
        {
            return logs.Where(x => x.Write.EventId.Name == "OutgoingTaskPaused").Count() == 2
                && logs.Where(x => x.Write.EventId.Name == "OutgoingTaskPauseAck").SingleOrDefault() != null;
        }))
        {
            var ccm = new TestClientConnectionManager();
            var ccf = new ClientConnectionFactory(loggerFactory);
            var serviceProtocol = new ServiceProtocol();
            var hubProtocol = new JsonHubProtocol();
            TestConnection transportConnection = null;
            var connectionFactory = new TestConnectionFactory(conn =>
            {
                transportConnection = conn;
                return Task.CompletedTask;
            });
            var services = new ServiceCollection();
            var builder = new ConnectionBuilder(services.BuildServiceProvider());
            builder.UseConnectionHandler<TestConnectionHandler>();
            var handler = builder.Build();

            var serviceConnection = CreateServiceConnection(serviceProtocol, hubProtocol, ccm, ccf, connectionFactory, loggerFactory, handler);
            var connectionTask = serviceConnection.StartAsync();

            // completed handshake
            await serviceConnection.ConnectionInitializedTask.OrTimeout();
            Assert.Equal(ServiceConnectionStatus.Connected, serviceConnection.Status);

            // wait for a client connection
            var clientConnection = await CreateClientConnectionAsync(serviceProtocol, hubProtocol, ccm, transportConnection);

            // send 2 pause message, expect only 1 pause ack message
            var pauseMessage = new ConnectionFlowControlMessage(clientConnection.ConnectionId, ConnectionFlowControlOperation.Pause, ConnectionType.Client);
            await transportConnection.Application.Output.WriteAsync(serviceProtocol.GetMessageBytes(pauseMessage));
            await transportConnection.Application.Output.WriteAsync(serviceProtocol.GetMessageBytes(pauseMessage));

            // read pause ack message
            var result = await transportConnection.Application.Input.ReadAsync().OrTimeout();
            var buffer = result.Buffer;
            Assert.True(serviceProtocol.TryParseMessage(ref buffer, out var m));
            var pauseAckMessage = Assert.IsType<ConnectionFlowControlMessage>(m);
            Assert.Equal(pauseMessage.ConnectionId, pauseAckMessage.ConnectionId);
            Assert.Equal(ConnectionFlowControlOperation.PauseAck, pauseAckMessage.Operation);
            Assert.Equal(ConnectionType.Client, pauseAckMessage.ConnectionType);

            // complete reading to end the connection
            transportConnection.Application.Output.Complete();

            await connectionTask.OrTimeout();
            Assert.Equal(ServiceConnectionStatus.Disconnected, serviceConnection.Status);
        }
    }

    [Fact]
    public async Task TestServiceConnectionHandleResumeMessageTask()
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Information, logChecker: logs =>
        {
            return logs.Where(x => x.Write.EventId.Name == "OutgoingTaskResume").SingleOrDefault() != null;
        }))
        {
            var ccm = new TestClientConnectionManager();
            var ccf = new ClientConnectionFactory(loggerFactory);
            var protocol = new ServiceProtocol();
            var hubProtocol = new JsonHubProtocol();
            TestConnection transportConnection = null;
            var connectionFactory = new TestConnectionFactory(conn =>
            {
                transportConnection = conn;
                return Task.CompletedTask;
            });
            var services = new ServiceCollection();
            var builder = new ConnectionBuilder(services.BuildServiceProvider());
            builder.UseConnectionHandler<TestConnectionHandler>();
            var handler = builder.Build();

            var serviceConnection = CreateServiceConnection(protocol, hubProtocol, ccm, ccf, connectionFactory, loggerFactory, handler);
            var connectionTask = serviceConnection.StartAsync();

            // completed handshake
            await serviceConnection.ConnectionInitializedTask.OrTimeout();
            Assert.Equal(ServiceConnectionStatus.Connected, serviceConnection.Status);

            // wait for a client connection
            var clientConnection = await CreateClientConnectionAsync(protocol, hubProtocol, ccm, transportConnection);

            // send resume message
            var message = new ConnectionFlowControlMessage(clientConnection.ConnectionId, ConnectionFlowControlOperation.Resume, ConnectionType.Client);
            await transportConnection.Application.Output.WriteAsync(protocol.GetMessageBytes(message));

            // complete reading to end the connection
            transportConnection.Application.Output.Complete();

            await connectionTask.OrTimeout();
            Assert.Equal(ServiceConnectionStatus.Disconnected, serviceConnection.Status);
        }
    }

    [Fact]
    public async Task TestServiceConnectionWithNormalApplicationTask()
    {
        using (StartVerifiableLog(out var loggerFactory))
        {
            var ccm = new TestClientConnectionManager();
            var ccf = new ClientConnectionFactory(loggerFactory);
            var protocol = new ServiceProtocol();
            var hubProtocol = new JsonHubProtocol();
            TestConnection transportConnection = null;
            var connectionFactory = new TestConnectionFactory(conn =>
            {
                transportConnection = conn;
                return Task.CompletedTask;
            });
            var services = new ServiceCollection();
            var builder = new ConnectionBuilder(services.BuildServiceProvider());
            builder.UseConnectionHandler<TestConnectionHandler>();
            var handler = builder.Build();
            var connection = new ServiceConnection(
                protocol, ccm, connectionFactory, loggerFactory, handler, ccf,
                "serverId", Guid.NewGuid().ToString("N"), null, null, null, new DefaultClientInvocationManager(),
                new DefaultHubProtocolResolver(new[] { hubProtocol }, NullLogger<DefaultHubProtocolResolver>.Instance));

            var connectionTask = connection.StartAsync();

            // completed handshake
            await connection.ConnectionInitializedTask.OrTimeout();
            Assert.Equal(ServiceConnectionStatus.Connected, connection.Status);
            var clientConnectionId = Guid.NewGuid().ToString();

            var waitClientTask = ccm.WaitForClientConnectionAsync(clientConnectionId);
            await transportConnection.Application.Output.WriteAsync(
                protocol.GetMessageBytes(new OpenConnectionMessage(clientConnectionId, new Claim[0] { }) { Protocol = hubProtocol.Name }));
            var clientConnection = await waitClientTask.OrTimeout();

            await transportConnection.Application.Output.WriteAsync(
                protocol.GetMessageBytes(new CloseConnectionMessage(clientConnectionId)));

            // Normal end with close message
            await ccm.WaitForClientConnectionRemovalAsync(clientConnectionId).OrTimeout();

            // another connection comes in
            clientConnectionId = Guid.NewGuid().ToString();

            waitClientTask = ccm.WaitForClientConnectionAsync(clientConnectionId);
            await transportConnection.Application.Output.WriteAsync(
                protocol.GetMessageBytes(new OpenConnectionMessage(clientConnectionId, new Claim[] { }) { Protocol = hubProtocol.Name }));

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
            var ccf = new ClientConnectionFactory(loggerFactory);
            var protocol = new ServiceProtocol();
            var hubProtocol = new JsonHubProtocol();
            TestConnection transportConnection = null;
            var connectionFactory = new TestConnectionFactory(conn =>
            {
                transportConnection = conn;
                return Task.CompletedTask;
            });
            var services = new ServiceCollection();
            var builder = new ConnectionBuilder(services.BuildServiceProvider());
            builder.UseConnectionHandler<TestConnectionHandler>();
            var handler = builder.Build();
            var connection = new ServiceConnection(
                protocol, ccm, connectionFactory, loggerFactory, handler, ccf,
                "serverId", Guid.NewGuid().ToString("N"), null, null, null, new DefaultClientInvocationManager(),
                new DefaultHubProtocolResolver(new[] { hubProtocol }, NullLogger<DefaultHubProtocolResolver>.Instance));

            var connectionTask = connection.StartAsync();

            // completed handshake
            await connection.ConnectionInitializedTask.OrTimeout();
            Assert.Equal(ServiceConnectionStatus.Connected, connection.Status);
            var clientConnectionId = Guid.NewGuid().ToString();
            var waitClientTask = ccm.WaitForClientConnectionAsync(clientConnectionId);
            await transportConnection.Application.Output.WriteAsync(
                protocol.GetMessageBytes(new OpenConnectionMessage(clientConnectionId, new Claim[] { }) { Protocol = hubProtocol.Name }));

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
            var ccf = new ClientConnectionFactory(loggerFactory);
            var protocol = new ServiceProtocol();
            var hubProtocol = new JsonHubProtocol();
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
            var handler = builder.Build();

            var connection = new ServiceConnection(
                protocol, ccm, connectionFactory, loggerFactory, handler, ccf,
                "serverId", Guid.NewGuid().ToString("N"), null, null, null, new DefaultClientInvocationManager(),
                new DefaultHubProtocolResolver(new[] { hubProtocol }, NullLogger<DefaultHubProtocolResolver>.Instance));

            var connectionTask = connection.StartAsync();

            // completed handshake
            await connection.ConnectionInitializedTask.OrTimeout();
            Assert.Equal(ServiceConnectionStatus.Connected, connection.Status);
            var clientConnectionId = Guid.NewGuid().ToString();
            var waitClientTask = ccm.WaitForClientConnectionAsync(clientConnectionId);
            await transportConnection.Application.Output.WriteAsync(
                protocol.GetMessageBytes(new OpenConnectionMessage(clientConnectionId, new Claim[] { }) { Protocol = hubProtocol.Name }));

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
                Assert.Equal($"The connection {clientConnectionId} has a long running application logic that prevents the connection from complete after 1 milliseconds.", logs[0].Write.Message);
                return true;
            }))
        {
            var ccm = new TestClientConnectionManager();
            var ccf = new ClientConnectionFactory(loggerFactory, closeTimeOutMilliseconds: 1);
            var protocol = new ServiceProtocol();
            var hubProtocol = new JsonHubProtocol();
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
            var handler = builder.Build();
            var hubProtocolResolver = new DefaultHubProtocolResolver(new[] { hubProtocol }, NullLogger<DefaultHubProtocolResolver>.Instance);
            var connection = new ServiceConnection(protocol,
                                                   ccm,
                                                   connectionFactory,
                                                   loggerFactory,
                                                   handler,
                                                   ccf,
                                                   "serverId",
                                                   Guid.NewGuid().ToString("N"),
                                                   null,
                                                   null,
                                                   null,
                                                   new DefaultClientInvocationManager(),
                                                   hubProtocolResolver);

            var connectionTask = connection.StartAsync();

            // completed handshake
            await connection.ConnectionInitializedTask.OrTimeout();
            Assert.Equal(ServiceConnectionStatus.Connected, connection.Status);
            var waitClientTask = ccm.WaitForClientConnectionAsync(clientConnectionId);
            await transportConnection.Application.Output.WriteAsync(
                protocol.GetMessageBytes(new OpenConnectionMessage(clientConnectionId, new Claim[] { }) { Protocol = hubProtocol.Name }));

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
    public async Task TestClientConnectionOutgoingAbortCanEndLifeTime()
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning, expectedErrors: c => true,
            logChecker: logs =>
            {
                // Cancel does not need to throw
                Assert.Empty(logs);
                return true;
            }))
        {
            var ccm = new TestClientConnectionManager();
            var ccf = new ClientConnectionFactory(loggerFactory, closeTimeOutMilliseconds: 500);
            var protocol = new ServiceProtocol();
            var hubProtocol = new JsonHubProtocol();
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
            var handler = builder.Build();
            var hubProtocolResolver = new DefaultHubProtocolResolver(new[] { hubProtocol }, NullLogger<DefaultHubProtocolResolver>.Instance);
            var connection = new ServiceConnection(protocol,
                                                   ccm,
                                                   connectionFactory,
                                                   loggerFactory,
                                                   handler,
                                                   ccf,
                                                   "serverId",
                                                   Guid.NewGuid().ToString("N"),
                                                   null,
                                                   null,
                                                   null,
                                                   new DefaultClientInvocationManager(),
                                                   hubProtocolResolver);

            var connectionTask = connection.StartAsync();

            // completed handshake
            await connection.ConnectionInitializedTask.OrTimeout();
            Assert.Equal(ServiceConnectionStatus.Connected, connection.Status);
            var clientConnectionId = Guid.NewGuid().ToString();
            var waitClientTask = ccm.WaitForClientConnectionAsync(clientConnectionId);
            await transportConnection.Application.Output.WriteAsync(
                protocol.GetMessageBytes(new OpenConnectionMessage(clientConnectionId, new Claim[] { }) { Protocol = hubProtocol.Name }));

            var context = await waitClientTask.OrTimeout();

            context.CancelOutgoing();

            connectionHandler.CancellationToken.Cancel();
            await context.LifetimeTask.OrTimeout();

            // complete reading to end the connection
            transportConnection.Application.Output.Complete();

            // 1s for application task to timeout
            await connectionTask.OrTimeout(1000);
            Assert.Equal(ServiceConnectionStatus.Disconnected, connection.Status);
            Assert.Empty(ccm.ClientConnections);
        }
    }

    [Fact]
    public async Task TestClientConnectionContextAbortCanSendOutCloseMessage()
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning, expectedErrors: c => true,
            logChecker: logs => true))
        {
            var ccm = new TestClientConnectionManager();
            var ccf = new ClientConnectionFactory(loggerFactory, closeTimeOutMilliseconds: 500);
            var protocol = new ServiceProtocol();
            var hubProtocol = new JsonHubProtocol();
            TestConnection transportConnection = null;
            var connectionFactory = new TestConnectionFactory(conn =>
            {
                transportConnection = conn;
                return Task.CompletedTask;
            });

            var services = new ServiceCollection();
            var lastWill = "This is the last will";
            var connectionHandler = new LastWillConnectionHandler(hubProtocol, lastWill);
            services.AddSingleton(connectionHandler);
            var builder = new ConnectionBuilder(services.BuildServiceProvider());
            builder.UseConnectionHandler<LastWillConnectionHandler>();
            var handler = builder.Build();

            var hubProtocolResolver = new DefaultHubProtocolResolver(new[] { hubProtocol }, NullLogger<DefaultHubProtocolResolver>.Instance);
            var connection = new ServiceConnection(protocol,
                                                   ccm,
                                                   connectionFactory,
                                                   loggerFactory,
                                                   handler,
                                                   ccf,
                                                   "serverId",
                                                   Guid.NewGuid().ToString("N"),
                                                   null,
                                                   null,
                                                   null,
                                                   new DefaultClientInvocationManager(),
                                                   hubProtocolResolver);

            var connectionTask = connection.StartAsync();

            // completed handshake
            await connection.ConnectionInitializedTask.OrTimeout();
            Assert.Equal(ServiceConnectionStatus.Connected, connection.Status);
            var clientConnectionId = Guid.NewGuid().ToString();

            // make sure to register for wait first
            var waitClientTask = ccm.WaitForClientConnectionAsync(clientConnectionId);

            await transportConnection.Application.Output.WriteAsync(
                protocol.GetMessageBytes(new OpenConnectionMessage(clientConnectionId, new Claim[] { }) { Protocol = hubProtocol.Name }));
            var clientConnection = await waitClientTask.OrTimeout();

            await clientConnection.LifetimeTask.OrTimeout();

            transportConnection.Transport.Output.Complete();
            var input = await transportConnection.Application.Input.ReadAsync();
            var buffer = input.Buffer;
            Assert.True(protocol.TryParseMessage(ref buffer, out var msg));
            var message = Assert.IsType<ConnectionDataMessage>(msg);
            Assert.Equal(DataMessageType.Handshake, message.Type);
            Assert.Equal(clientConnectionId, message.ConnectionId);
            Assert.Equal("{}\u001e", Encoding.UTF8.GetString(message.Payload.ToArray()));

            Assert.True(protocol.TryParseMessage(ref buffer, out msg));
            message = Assert.IsType<ConnectionDataMessage>(msg);
            Assert.Equal(DataMessageType.Invocation, message.Type);
            Assert.Equal(clientConnectionId, message.ConnectionId);
            Assert.Equal($"{{\"type\":1,\"target\":\"{lastWill}\",\"arguments\":[]}}\u001e", Encoding.UTF8.GetString(message.Payload.ToArray()));

            // complete reading to end the connection
            transportConnection.Application.Output.Complete();

            // 1s for application task to timeout
            await connectionTask.OrTimeout(1000);
            Assert.Equal(ServiceConnectionStatus.Disconnected, connection.Status);
            Assert.Empty(ccm.ClientConnections);
        }
    }

    [Fact]
    public async Task TestClientConnectionWithDiagnosticClientTagTest()
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
        {
            var ccm = new TestClientConnectionManager();
            var ccf = new ClientConnectionFactory(loggerFactory, closeTimeOutMilliseconds: 500);
            var protocol = new ServiceProtocol();
            var hubProtocol = new JsonHubProtocol();
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
            var handler = builder.Build();

            var defaultHubProtocolResolver = new DefaultHubProtocolResolver(new[] { hubProtocol }, NullLogger<DefaultHubProtocolResolver>.Instance);
            var connection = new ServiceConnection(protocol,
                                                   ccm,
                                                   connectionFactory,
                                                   loggerFactory,
                                                   handler,
                                                   ccf,
                                                   "serverId",
                                                   Guid.NewGuid().ToString("N"),
                                                   null,
                                                   null,
                                                   null,
                                                   new DefaultClientInvocationManager(),
                                                   defaultHubProtocolResolver);

            var connectionTask = connection.StartAsync();

            // completed handshake
            await connection.ConnectionInitializedTask.OrTimeout();
            Assert.Equal(ServiceConnectionStatus.Connected, connection.Status);
            var waitClientTask = Task.WhenAll(ccm.WaitForClientConnectionAsync(normalClientConnectionId),
                ccm.WaitForClientConnectionAsync(diagnosticClientConnectionId));
            await transportConnection.Application.Output.WriteAsync(
                    protocol.GetMessageBytes(
                        new OpenConnectionMessage(
                            diagnosticClientConnectionId,
                            null,
                            new Dictionary<string, StringValues>
                            {
                                { Constants.AsrsIsDiagnosticClient, "true"}
                            },
                            null)
                        {
                            Protocol = hubProtocol.Name
                        }));

            await transportConnection.Application.Output.WriteAsync(
                protocol.GetMessageBytes(new OpenConnectionMessage(normalClientConnectionId, null) { Protocol = hubProtocol.Name }));

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

    [Theory]
    [InlineData(Constants.AsrsMigrateFrom, true)]
    [InlineData("anotherheader", false)]
    public async Task TestClientConnectionShouldSkipHandshakeWhenMigrateIn(string headerKey, bool shoudSkip)
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning, logChecker: logs => true))
        {
            var ccm = new TestClientConnectionManager();
            var ccf = new ClientConnectionFactory(loggerFactory, closeTimeOutMilliseconds: 500);
            var protocol = new ServiceProtocol();
            var hubProtocol = new JsonHubProtocol();

            TestConnection serviceConnection = null;
            var connectionFactory = new TestConnectionFactory(conn =>
            {
                serviceConnection = conn;
                return Task.CompletedTask;
            });
            var services = new ServiceCollection();

            services.AddSingleton<ConnectionHandler, EndlessConnectionHandler>();
            var builder = new ConnectionBuilder(services.BuildServiceProvider());
            builder.UseConnectionHandler<EndlessConnectionHandler>();
            var handler = builder.Build();
            var defaultHubProtocolResolver = new DefaultHubProtocolResolver(new[] { hubProtocol }, NullLogger<DefaultHubProtocolResolver>.Instance);
            var connection = new ServiceConnection(protocol,
                                                   ccm,
                                                   connectionFactory,
                                                   loggerFactory,
                                                   handler,
                                                   ccf,
                                                   "serverId",
                                                   Guid.NewGuid().ToString("N"),
                                                   null,
                                                   null,
                                                   null,
                                                   new DefaultClientInvocationManager(),
                                                   defaultHubProtocolResolver);

            var connectionTask = connection.StartAsync();
            await connection.ConnectionInitializedTask.OrTimeout();
            Assert.Equal(ServiceConnectionStatus.Connected, connection.Status);

            // open a new connection with migrate header.
            var clientConnectionId = Guid.NewGuid().ToString();
            await serviceConnection.Application.Output.WriteAsync(
                protocol.GetMessageBytes(new OpenConnectionMessage(clientConnectionId, Array.Empty<Claim>())
                {
                    Protocol = "json",
                    Headers = new Dictionary<string, StringValues>
                    {
                        [headerKey] = "serverId"
                    }
                }));

            // send a handshake response, should be skipped.
            var context = await ccm.WaitForClientConnectionAsync(clientConnectionId).OrTimeout();
            var handshakeResponse = new SignalRProtocol.HandshakeResponseMessage(null);
            HandshakeProtocol.WriteResponseMessage(handshakeResponse, context.Transport.Output);
            await context.Transport.Output.FlushAsync();

            // send a test message.
            var payload = Encoding.UTF8.GetBytes("{\"type\":1,\"target\":\"method\",\"arguments\":[]}\u001e");
            await context.Transport.Output.WriteAsync(payload).OrTimeout();

            var result = await serviceConnection.Application.Input.ReadAsync().OrTimeout();
            var buffer = result.Buffer;
            Assert.True(protocol.TryParseMessage(ref buffer, out var message));
            var dataMessage = Assert.IsType<ConnectionDataMessage>(message);

            if (shoudSkip)
            {
                Assert.Equal(payload, dataMessage.Payload.ToArray());
            }
            else
            {
                var dataPayload = dataMessage.Payload;
                Assert.True(HandshakeProtocol.TryParseResponseMessage(ref dataPayload, out _));
            }

            serviceConnection.Application.Output.Complete();
            await connectionTask.OrTimeout();
        }
    }

    [Fact]
    public async Task TestClientConnectionLastWillCanSendOut()
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning, expectedErrors: c => true,
            logChecker: logs =>
            {
                Assert.Empty(logs);
                return true;
            }))
        {
            var ccm = new TestClientConnectionManager();
            var ccf = new ClientConnectionFactory(loggerFactory, closeTimeOutMilliseconds: 1000);
            var protocol = new ServiceProtocol();
            var hubProtocol = new JsonHubProtocol();
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
            var handler = builder.Build();
            var hubProtocolResolver = new DefaultHubProtocolResolver(new[] { hubProtocol }, NullLogger<DefaultHubProtocolResolver>.Instance);
            var connection = new ServiceConnection(protocol,
                                                   ccm,
                                                   connectionFactory,
                                                   loggerFactory,
                                                   handler,
                                                   ccf,
                                                   "serverId",
                                                   Guid.NewGuid().ToString("N"),
                                                   null,
                                                   null,
                                                   null,
                                                   new DefaultClientInvocationManager(),
                                                   hubProtocolResolver);

            var connectionTask = connection.StartAsync();

            // completed handshake
            await connection.ConnectionInitializedTask.OrTimeout();
            Assert.Equal(ServiceConnectionStatus.Connected, connection.Status);
            var clientConnectionId = Guid.NewGuid().ToString();

            await transportConnection.Application.Output.WriteAsync(
                protocol.GetMessageBytes(new OpenConnectionMessage(clientConnectionId, Array.Empty<Claim>()) { Protocol = hubProtocol.Name }));

            var clientConnection = await ccm.WaitForClientConnectionAsync(clientConnectionId).OrTimeout();

            // complete reading to end the connection
            transportConnection.Application.Output.Complete();

            connectionHandler.CancellationToken.Cancel();

            await clientConnection.LifetimeTask.OrTimeout();

            await connectionTask.OrTimeout();
            Assert.Equal(ServiceConnectionStatus.Disconnected, connection.Status);
            Assert.Empty(ccm.ClientConnections);
        }
    }

    [Fact]
    public async Task TestPartialMessagesShouldFlushCorrectly()
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning, expectedErrors: c => true,
            logChecker: logs =>
            {
                Assert.Empty(logs);
                return true;
            }))
        {
            var ccm = new TestClientConnectionManager();
            var ccf = new ClientConnectionFactory(loggerFactory, closeTimeOutMilliseconds: 500);
            var protocol = new ServiceProtocol();
            var hubProtocol = new JsonHubProtocol();
            TestConnection transportConnection = null;
            var connectionFactory = new TestConnectionFactory(conn =>
            {
                transportConnection = conn;
                return Task.CompletedTask;
            });
            var services = new ServiceCollection();

            var connectionHandler = new TextContentConnectionHandler();
            services.AddSingleton(connectionHandler);
            var builder = new ConnectionBuilder(services.BuildServiceProvider());
            builder.UseConnectionHandler<TextContentConnectionHandler>();
            var handler = builder.Build();
            var hubProcotolResolver = new DefaultHubProtocolResolver(new[] { hubProtocol }, NullLogger<DefaultHubProtocolResolver>.Instance);
            var connection = new ServiceConnection(protocol,
                                                   ccm,
                                                   connectionFactory,
                                                   loggerFactory,
                                                   handler,
                                                   ccf,
                                                   "serverId",
                                                   Guid.NewGuid().ToString("N"),
                                                   null,
                                                   null,
                                                   null,
                                                   new DefaultClientInvocationManager(),
                                                   hubProcotolResolver);

            var connectionTask = connection.StartAsync().OrTimeout();

            // completed handshake
            await connection.ConnectionInitializedTask.OrTimeout();
            Assert.Equal(ServiceConnectionStatus.Connected, connection.Status);
            var clientConnectionId = Guid.NewGuid().ToString();

            var waitClientTask = ccm.WaitForClientConnectionAsync(clientConnectionId);
            await transportConnection.Application.Output.WriteAsync(
                protocol.GetMessageBytes(new OpenConnectionMessage(clientConnectionId, new Claim[] { }) { Protocol = hubProtocol.Name }));

            var clientConnection = await waitClientTask.OrTimeout();

            var enumerator = connectionHandler.EnumerateContent().GetAsyncEnumerator();

            // for normal message, it should flush immediately.
            await transportConnection.Application.Output.WriteAsync(
                protocol.GetMessageBytes(new ConnectionDataMessage(clientConnectionId, Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e"))));
            await enumerator.MoveNextAsync();
            Assert.Equal("{\"protocol\":\"json\",\"version\":1}\u001e", enumerator.Current);

            // for partial message, it should wait for next message to complete.
            await transportConnection.Application.Output.WriteAsync(
                protocol.GetMessageBytes(new ConnectionDataMessage(clientConnectionId, Encoding.UTF8.GetBytes("{\"type\":1,")) { IsPartial = true }));
            var delay = Task.Delay(100);
            var moveNextTask = enumerator.MoveNextAsync().AsTask();
            Assert.Same(delay, await Task.WhenAny(delay, moveNextTask));

            // when next message comes, it should complete the previous partial message.
            await transportConnection.Application.Output.WriteAsync(
                protocol.GetMessageBytes(new ConnectionDataMessage(clientConnectionId, Encoding.UTF8.GetBytes("\"target\":\"method\"}\u001e"))));
            await moveNextTask;
            if (enumerator.Current == "{\"type\":1,")
            {
                Assert.Equal("{\"type\":1,", enumerator.Current);
                await enumerator.MoveNextAsync();
                Assert.Equal("\"target\":\"method\"}\u001e", enumerator.Current);
            }
            else
            {
                // maybe merged into one message.
                Assert.Equal("{\"type\":1,\"target\":\"method\"}\u001e", enumerator.Current);
            }

            // complete reading to end the connection
            transportConnection.Application.Output.Complete();

            await clientConnection.LifetimeTask.OrTimeout();

            // 1s for application task to timeout
            await connectionTask.OrTimeout(1000);
            Assert.Equal(ServiceConnectionStatus.Disconnected, connection.Status);
            Assert.Empty(ccm.ClientConnections);
        }
    }

    [Fact]
    public async Task TestPartialMessagesShouldBeRemovedWhenReconnected()
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning, expectedErrors: c => true,
            logChecker: logs =>
            {
                Assert.Empty(logs);
                return true;
            }))
        {
            var ccm = new TestClientConnectionManager();
            var ccf = new ClientConnectionFactory(loggerFactory, closeTimeOutMilliseconds: 500);
            var protocol = new ServiceProtocol();
            var hubProtocol = new JsonHubProtocol();
            TestConnection transportConnection = null;
            var connectionFactory = new TestConnectionFactory(conn =>
            {
                transportConnection = conn;
                return Task.CompletedTask;
            });
            var services = new ServiceCollection();

            var connectionHandler = new TextContentConnectionHandler();
            services.AddSingleton(connectionHandler);
            var builder = new ConnectionBuilder(services.BuildServiceProvider());
            builder.UseConnectionHandler<TextContentConnectionHandler>();
            var handler = builder.Build();
            var hubProtocolResolver = new DefaultHubProtocolResolver(new[] { hubProtocol }, NullLogger<DefaultHubProtocolResolver>.Instance);
            var connection = new ServiceConnection(protocol,
                                                   ccm,
                                                   connectionFactory,
                                                   loggerFactory,
                                                   handler,
                                                   ccf,
                                                   "serverId",
                                                   Guid.NewGuid().ToString("N"),
                                                   null,
                                                   null,
                                                   null,
                                                   new DefaultClientInvocationManager(),
                                                   hubProtocolResolver);

            var connectionTask = connection.StartAsync();

            // completed handshake
            await connection.ConnectionInitializedTask.OrTimeout();
            Assert.Equal(ServiceConnectionStatus.Connected, connection.Status);
            var clientConnectionId = Guid.NewGuid().ToString();

            var waitClientTask = ccm.WaitForClientConnectionAsync(clientConnectionId);
            await transportConnection.Application.Output.WriteAsync(
                protocol.GetMessageBytes(new OpenConnectionMessage(clientConnectionId, new Claim[] { }) { Protocol = hubProtocol.Name }));

            var clientConnection = await waitClientTask.OrTimeout();

            var enumerator = connectionHandler.EnumerateContent().GetAsyncEnumerator();

            // for partial message, it should wait for next message to complete.
            await transportConnection.Application.Output.WriteAsync(
                protocol.GetMessageBytes(new ConnectionDataMessage(clientConnectionId, Encoding.UTF8.GetBytes("{\"type\":1,")) { IsPartial = true }));
            var delay = Task.Delay(100);
            var moveNextTask = enumerator.MoveNextAsync().AsTask();
            Assert.Same(delay, await Task.WhenAny(delay, moveNextTask));

            // for reconnect message, it should remove all partial messages for the connection.
            await transportConnection.Application.Output.WriteAsync(
                protocol.GetMessageBytes(new ConnectionReconnectMessage(clientConnectionId)));
            delay = Task.Delay(100);
            Assert.Same(delay, await Task.WhenAny(delay, moveNextTask));

            // when next message comes, there is no partial message.
            await transportConnection.Application.Output.WriteAsync(
                protocol.GetMessageBytes(new ConnectionDataMessage(clientConnectionId, Encoding.UTF8.GetBytes("{\"type\":1,\"target\":\"method\"}\u001e"))));
            await moveNextTask;
            Assert.Equal("{\"type\":1,\"target\":\"method\"}\u001e", enumerator.Current);

            // complete reading to end the connection
            transportConnection.Application.Output.Complete();

            await clientConnection.LifetimeTask.OrTimeout();

            // 1s for application task to timeout
            await connectionTask.OrTimeout(1000);
            Assert.Equal(ServiceConnectionStatus.Disconnected, connection.Status);
            Assert.Empty(ccm.ClientConnections);
        }
    }

    private static async Task<ClientConnectionContext> CreateClientConnectionAsync(ServiceProtocol protocol,
                                                                                   IHubProtocol hubProtocol,
                                                                                   TestClientConnectionManager ccm,
                                                                                   TestConnection transportConnection)
    {
        var clientConnectionId = Guid.NewGuid().ToString();
        var waitClientTask = ccm.WaitForClientConnectionAsync(clientConnectionId);
        var openConnectionMessage = new OpenConnectionMessage(clientConnectionId, Array.Empty<Claim>()) { Protocol = hubProtocol.Name };
        await transportConnection.Application.Output.WriteAsync(protocol.GetMessageBytes(openConnectionMessage));
        return await waitClientTask;
    }

    private static ServiceConnection CreateServiceConnection(ServiceProtocol protocol,
                                                             IHubProtocol hubProtocol,
                                                             IClientConnectionManager ccm,
                                                             IClientConnectionFactory ccf,
                                                             IConnectionFactory cf,
                                                             ILoggerFactory loggerFactory,
                                                             ConnectionDelegate connectionDelegate)
    {
        return new ServiceConnection(protocol,
                                     ccm,
                                     cf,
                                     loggerFactory,
                                     connectionDelegate,
                                     ccf,
                                     "serverId",
                                     Guid.NewGuid().ToString("N"),
                                     null,
                                     null,
                                     null,
                                     new DefaultClientInvocationManager(),
                                     new DefaultHubProtocolResolver(new[] { hubProtocol }, NullLogger<DefaultHubProtocolResolver>.Instance));
    }

    private sealed class TestConnectionHandler : ConnectionHandler
    {
        private readonly TaskCompletionSource<object> _startedTcs = new TaskCompletionSource<object>();

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
        private readonly IHubProtocol _hubProtocol;

        private readonly string _lastWill;

        public LastWillConnectionHandler(IHubProtocol hubProtocol, string lastWill)
        {
            _hubProtocol = hubProtocol;
            _lastWill = lastWill;
        }

        public override async Task OnConnectedAsync(ConnectionContext connection)
        {
            HandshakeProtocol.WriteResponseMessage(SignalRProtocol.HandshakeResponseMessage.Empty, connection.Transport.Output);
            _hubProtocol.WriteMessage(new InvocationMessage(_lastWill, new object[0]), connection.Transport.Output);
            await connection.Transport.Output.FlushAsync();
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
        private readonly string _diagnosticClient;

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

    private sealed class TextContentConnectionHandler : ConnectionHandler
    {
        private readonly TaskCompletionSource<object> _startedTcs = new TaskCompletionSource<object>();

        private readonly LinkedList<TaskCompletionSource<string>> _content = new LinkedList<TaskCompletionSource<string>>();

        public Task Started => _startedTcs.Task;

        public TextContentConnectionHandler()
        {
            _content.AddFirst(new TaskCompletionSource<string>());
        }

        public override async Task OnConnectedAsync(ConnectionContext connection)
        {
            _startedTcs.TrySetResult(null);

            while (true)
            {
                var result = await connection.Transport.Input.ReadAsync();

                try
                {
                    if (!result.Buffer.IsEmpty)
                    {
                        var last = _content.Last.Value;
                        _content.AddLast(new TaskCompletionSource<string>());
                        var text = Encoding.UTF8.GetString(result.Buffer);
                        last.TrySetResult(text);
                    }

                    if (result.IsCompleted)
                    {
                        _content.Last.Value.TrySetResult(null);
                        break;
                    }
                }
                finally
                {
                    connection.Transport.Input.AdvanceTo(result.Buffer.End);
                }
            }
        }

        public async IAsyncEnumerable<string> EnumerateContent()
        {
            while (true)
            {
                var result = await _content.First.Value.Task;
                _content.RemoveFirst();
                if (result == null)
                {
                    yield break;
                }
                yield return result;
            }
        }
    }
}
