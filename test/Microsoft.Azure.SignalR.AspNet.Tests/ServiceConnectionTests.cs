// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Transports;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.AspNet.Tests;

public class ServiceConnectionTests : VerifiableLoggedTest
{
    private const string ConnectionString = "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;";

    public ServiceConnectionTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task ServiceConnectionDispatchTest()
    {
        int count = 0;
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
        {
            var hubConfig = Utility.GetTestHubConfig(loggerFactory);
            var atm = new AzureTransportManager(hubConfig.Resolver);
            hubConfig.Resolver.Register(typeof(ITransportManager), () => atm);

            var clientConnectionManager = new TestClientConnectionManager();
            using (var proxy = new TestServiceConnectionProxy(clientConnectionManager, loggerFactory: loggerFactory))
            {
                // start the server connection
                await proxy.StartServiceAsync().OrTimeout();

                var clientConnection = Guid.NewGuid().ToString("N");

                // Application layer sends OpenConnectionMessage
                var openConnectionMessage = new OpenConnectionMessage(clientConnection, new Claim[0], null, "?transport=webSockets");
                var task = clientConnectionManager.WaitForClientConnectAsync(clientConnection).OrTimeout();
                await proxy.WriteMessageAsync(openConnectionMessage);
                await task;

                // Validate in transport for 1000 data messages.
                clientConnectionManager.CurrentTransports.TryGetValue(clientConnection, out var transport);
                Assert.NotNull(transport);

                while (count < 1000)
                {
                    await proxy.WriteMessageAsync(new ConnectionDataMessage(clientConnection, "Hello World".GenerateSingleFrameBuffer()));
                    count++;
                }

                await proxy.WriteMessageAsync(new CloseConnectionMessage(clientConnection));

                await transport.WaitOnDisconnected().OrTimeout();

                // Validate in transport for 1000 data messages.
                Assert.Equal(transport.MessageCount, count);

                Assert.Empty(clientConnectionManager.CurrentTransports);
            }
        }
    }

    [Fact]
    public async Task ServiceConnectionDispatchGroupMessagesTest()
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
        {
            var hubConfig = Utility.GetActualHubConfig(loggerFactory);
            var appName = "app1";
            var hub = "chat";
            var scm = new TestServiceConnectionHandler();
            hubConfig.Resolver.Register(typeof(IServiceConnectionManager), () => scm);
            var ccm = new ClientConnectionManager(hubConfig, loggerFactory);
            hubConfig.Resolver.Register(typeof(IClientConnectionManager), () => ccm);
            DispatcherHelper.PrepareAndGetDispatcher(new TestAppBuilder(), hubConfig, new ServiceOptions { ConnectionString = ConnectionString }, appName, loggerFactory);
            using (var proxy = new TestServiceConnectionProxy(ccm, loggerFactory: loggerFactory))
            {
                // start the server connection
                await proxy.StartServiceAsync().OrTimeout();

                var clientConnection = Guid.NewGuid().ToString("N");

                var connectTask = scm.WaitForTransportOutputMessageAsync(typeof(GroupBroadcastDataMessage)).OrTimeout();
                // Application layer sends OpenConnectionMessage
                var openConnectionMessage = new OpenConnectionMessage(clientConnection, new Claim[0], null, $"?transport=webSockets&connectionToken=conn1&connectionData=%5B%7B%22name%22%3A%22{hub}%22%7D%5D");
                await proxy.WriteMessageAsync(openConnectionMessage);

                var connectMessage = (await connectTask) as GroupBroadcastDataMessage;
                Assert.NotNull(connectMessage);
                Assert.Equal($"hg-{hub}.note", connectMessage.GroupName);

                var message = connectMessage.Payloads["json"].GetJsonMessageFromSingleFramePayload<HubResponseItem>();

                Assert.Equal("Connected", message.A[0]);

                // group message goes into the manager
                // make sure the tcs is called before writing message
                var jgTask = scm.WaitForTransportOutputMessageAsync(typeof(JoinGroupWithAckMessage)).OrTimeout();

                var gbTask = scm.WaitForTransportOutputMessageAsync(typeof(GroupBroadcastDataMessage)).OrTimeout();

                await proxy.WriteMessageAsync(new ConnectionDataMessage(clientConnection, Encoding.UTF8.GetBytes($"{{\"H\":\"{hub}\",\"M\":\"JoinGroup\",\"A\":[\"user1\",\"group1\"],\"I\":1}}")));

                var groupMessage = (await jgTask) as JoinGroupWithAckMessage;
                Assert.NotNull(groupMessage);
                Assert.Equal($"hg-{hub}.group1", groupMessage.GroupName);
                var broadcastMessage = (await gbTask) as GroupBroadcastDataMessage;
                Assert.NotNull(broadcastMessage);
                Assert.Equal($"hg-{hub}.group1", broadcastMessage.GroupName);

                var lgTask = scm.WaitForTransportOutputMessageAsync(typeof(LeaveGroupWithAckMessage)).OrTimeout();
                gbTask = scm.WaitForTransportOutputMessageAsync(typeof(GroupBroadcastDataMessage)).OrTimeout();

                await proxy.WriteMessageAsync(new ConnectionDataMessage(clientConnection, Encoding.UTF8.GetBytes($"{{\"H\":\"{hub}\",\"M\":\"LeaveGroup\",\"A\":[\"user1\",\"group1\"],\"I\":1}}")));

                var leaveGroupMessage = (await lgTask) as LeaveGroupWithAckMessage;
                Assert.NotNull(leaveGroupMessage);
                Assert.Equal($"hg-{hub}.group1", leaveGroupMessage.GroupName);

                broadcastMessage = (await gbTask) as GroupBroadcastDataMessage;
                Assert.NotNull(broadcastMessage);
                Assert.Equal($"hg-{hub}.group1", broadcastMessage.GroupName);

                var disconnectTask = scm.WaitForTransportOutputMessageAsync(typeof(GroupBroadcastDataMessage))
                    .OrTimeout();

                await proxy.WriteMessageAsync(new CloseConnectionMessage(clientConnection));

                var disconnectMessage = (await disconnectTask) as GroupBroadcastDataMessage;

                Assert.NotNull(disconnectMessage);
                Assert.Equal($"hg-{hub}.note", disconnectMessage.GroupName);

                message = disconnectMessage.Payloads["json"].GetJsonMessageFromSingleFramePayload<HubResponseItem>();

                Assert.Equal("Disconnected", message.A[0]);

                // cleaned up clearly
                Assert.Empty(ccm.ClientConnections);
            }
        }
    }

    [Fact]
    public async Task ServiceConnectionWithErrorConnectHub()
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning, expectedErrors: c=>true, logChecker:
            logs =>
            {
                Assert.Equal(2, logs.Count);
                Assert.Equal("ErrorExecuteConnected", logs[0].Write.EventId.Name);
                Assert.Equal("ConnectedStartingFailed", logs[1].Write.EventId.Name);
                return true;
            }))
        {
            var hubConfig = Utility.GetActualHubConfig(loggerFactory);
            var appName = "app1";
            var hub = "ErrorConnect"; // error connect hub
            var scm = new TestServiceConnectionHandler();
            hubConfig.Resolver.Register(typeof(IServiceConnectionManager), () => scm);
            var ccm = new ClientConnectionManager(hubConfig, loggerFactory);
            hubConfig.Resolver.Register(typeof(IClientConnectionManager), () => ccm);
            DispatcherHelper.PrepareAndGetDispatcher(new TestAppBuilder(), hubConfig, new ServiceOptions { ConnectionString = ConnectionString }, appName, loggerFactory);
            using (var proxy = new TestServiceConnectionProxy(ccm, loggerFactory: loggerFactory))
            {
                // start the server connection
                await proxy.StartServiceAsync().OrTimeout();

                var clientConnection = Guid.NewGuid().ToString("N");

                var connectTask = proxy.WaitForOutgoingMessageAsync(clientConnection).OrTimeout();

                // Application layer sends OpenConnectionMessage
                var openConnectionMessage = new OpenConnectionMessage(clientConnection, new Claim[0], null, $"?transport=webSockets&connectionToken=conn1&connectionData=%5B%7B%22name%22%3A%22{hub}%22%7D%5D");
                await proxy.WriteMessageAsync(openConnectionMessage);

                // other messages are just ignored because OnConnected failed
                await proxy.WriteMessageAsync(new ConnectionDataMessage(clientConnection, Encoding.UTF8.GetBytes($"{{\"H\":\"{hub}\",\"M\":\"JoinGroup\",\"A\":[\"user1\",\"group1\"],\"I\":1}}")));

                await proxy.WriteMessageAsync(new ConnectionDataMessage(clientConnection, Encoding.UTF8.GetBytes($"{{\"H\":\"{hub}\",\"M\":\"LeaveGroup\",\"A\":[\"user1\",\"group1\"],\"I\":1}}")));

                await proxy.WriteMessageAsync(new CloseConnectionMessage(clientConnection));

                var message = await connectTask;

                Assert.True(message is CloseConnectionMessage);

                // cleaned up clearly
                Assert.Empty(ccm.ClientConnections);
            }
        }
    }

    [Fact]
    public async Task ServiceConnectionWithErrorDisconnectHub()
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug, expectedErrors: c => true, logChecker:
            logs => true))
        {
            var hubConfig = Utility.GetActualHubConfig(loggerFactory);
            var appName = "app1";
            var hub = "ErrorDisconnect"; // error connect hub
            var scm = new TestServiceConnectionHandler();
            hubConfig.Resolver.Register(typeof(IServiceConnectionManager), () => scm);
            var ccm = new ClientConnectionManager(hubConfig, loggerFactory);
            hubConfig.Resolver.Register(typeof(IClientConnectionManager), () => ccm);
            DispatcherHelper.PrepareAndGetDispatcher(new TestAppBuilder(), hubConfig,
                new ServiceOptions {ConnectionString = ConnectionString}, appName, loggerFactory);
            using (var proxy = new TestServiceConnectionProxy(ccm, loggerFactory: loggerFactory))
            {
                // start the server connection
                await proxy.StartServiceAsync().OrTimeout();

                var clientConnection = Guid.NewGuid().ToString("N");

                var connectTask = scm.WaitForTransportOutputMessageAsync(typeof(GroupBroadcastDataMessage))
                    .OrTimeout();
                // Application layer sends OpenConnectionMessage
                var openConnectionMessage = new OpenConnectionMessage(clientConnection, new Claim[0], null,
                    $"?transport=webSockets&connectionToken=conn1&connectionData=%5B%7B%22name%22%3A%22{hub}%22%7D%5D");
                await proxy.WriteMessageAsync(openConnectionMessage);

                var connectMessage = (await connectTask) as GroupBroadcastDataMessage;
                Assert.NotNull(connectMessage);
                Assert.Equal($"hg-{hub}.note", connectMessage.GroupName);

                var message = connectMessage.Payloads["json"]
                    .GetJsonMessageFromSingleFramePayload<HubResponseItem>();

                Assert.Equal("Connected", message.A[0]);

                var disconnectTask = scm.WaitForTransportOutputMessageAsync(typeof(GroupBroadcastDataMessage))
                    .OrTimeout();

                await proxy.WriteMessageAsync(new CloseConnectionMessage(clientConnection));

                var disconnectMessage = (await disconnectTask) as GroupBroadcastDataMessage;

                Assert.NotNull(disconnectMessage);
                Assert.Equal($"hg-{hub}.note", disconnectMessage.GroupName);

                message = disconnectMessage.Payloads["json"]
                    .GetJsonMessageFromSingleFramePayload<HubResponseItem>();

                Assert.Equal("Disconnected", message.A[0]);

                // cleaned up clearly
                Assert.Empty(ccm.ClientConnections);
            }
        }
    }

    [Fact]
    public async Task ServiceConnectionDispatchOpenConnectionToUnauthorizedHubTest()
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning, expectedErrors: c => true, logChecker:
            logs =>
            {
                Assert.Single(logs);
                Assert.Equal("ConnectedStartingFailed", logs[0].Write.EventId.Name);
                Assert.Equal("Unable to authorize request", logs[0].Write.Exception.Message);
                return true;
            }))
        {
            var hubConfig = new HubConfiguration();
            var ccm = new ClientConnectionManager(hubConfig, loggerFactory);
            hubConfig.Resolver.Register(typeof(IClientConnectionManager), () => ccm);
            using (var proxy = new TestServiceConnectionProxy(ccm, loggerFactory: loggerFactory))
            {
                // start the server connection
                await proxy.StartServiceAsync().OrTimeout();

                var connectionId = Guid.NewGuid().ToString("N");
                var connectTask = proxy.WaitForOutgoingMessageAsync(connectionId).OrTimeout();

                // Application layer sends OpenConnectionMessage to an authorized hub from anonymous user
                var openConnectionMessage = new OpenConnectionMessage(connectionId, new Claim[0], null, "?transport=webSockets&connectionData=%5B%7B%22name%22%3A%22authchat%22%7D%5D");
                await proxy.WriteMessageAsync(openConnectionMessage);

                var message = await connectTask;

                Assert.True(message is CloseConnectionMessage);

                // Verify client connection is not created due to authorized failure.
                Assert.False(ccm.ClientConnections.TryGetValue(connectionId, out var connection));
            }
        }
    }

    [Fact]
    public async Task ServiceConnectionWithNormalClientConnection()
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning, expectedErrors: c => true, logChecker:
            logs =>
            {
                Assert.Single(logs);
                Assert.Equal("ConnectedStartingFailed", logs[0].Write.EventId.Name);
                Assert.Equal("Unable to authorize request", logs[0].Write.Exception.Message);
                return true;
            }))
        {
            var hubConfig = new HubConfiguration();
            var ccm = new ClientConnectionManager(hubConfig, loggerFactory);
            hubConfig.Resolver.Register(typeof(IClientConnectionManager), () => ccm);
            using (var proxy = new TestServiceConnectionProxy(ccm, loggerFactory: loggerFactory))
            {
                // start the server connection
                await proxy.StartServiceAsync().OrTimeout();

                var connectionId = Guid.NewGuid().ToString("N");

                var connectTask = proxy.WaitForOutgoingMessageAsync(connectionId).OrTimeout();

                // Application layer sends OpenConnectionMessage to an authorized hub from anonymous user
                var openConnectionMessage = new OpenConnectionMessage(connectionId, new Claim[0], null, "?transport=webSockets&connectionData=%5B%7B%22name%22%3A%22authchat%22%7D%5D");
                await proxy.WriteMessageAsync(openConnectionMessage);

                var message = await connectTask;

                Assert.True(message is CloseConnectionMessage);

                // Verify client connection is not created due to authorized failure.
                Assert.False(ccm.ClientConnections.TryGetValue(connectionId, out var connection));
            }
        }
    }

    [Theory]
    [InlineData("chat")]
    [InlineData("ErrorDisconnect")]
    public async Task ServiceConnectionWithTransportLayerClosedShouldCleanupNormalClientConnections(string hub)
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug, expectedErrors: c => true))
        {
            var hubConfig = Utility.GetActualHubConfig(loggerFactory);
            var appName = "app1";
            var scm = new TestServiceConnectionHandler();
            hubConfig.Resolver.Register(typeof(IServiceConnectionManager), () => scm);
            var ccm = new ClientConnectionManager(hubConfig, loggerFactory);
            hubConfig.Resolver.Register(typeof(IClientConnectionManager), () => ccm);
            DispatcherHelper.PrepareAndGetDispatcher(new TestAppBuilder(), hubConfig,
                new ServiceOptions { ConnectionString = ConnectionString }, appName, loggerFactory);
            using (var proxy = new TestServiceConnectionProxy(ccm, loggerFactory))
            {
                // start the server connection
                var connectionTask = proxy.StartAsync();
                await proxy.ConnectionInitializedTask.OrTimeout();

                var clientConnection = Guid.NewGuid().ToString("N");

                var connectTask = scm.WaitForTransportOutputMessageAsync(typeof(GroupBroadcastDataMessage))
                    .OrTimeout();
                // Application layer sends OpenConnectionMessage
                var openConnectionMessage = new OpenConnectionMessage(clientConnection, new Claim[0], null,
                    $"?transport=webSockets&connectionToken=conn1&connectionData=%5B%7B%22name%22%3A%22{hub}%22%7D%5D");
                await proxy.WriteMessageAsync(openConnectionMessage);

                var connectMessage = (await connectTask) as GroupBroadcastDataMessage;
                Assert.NotNull(connectMessage);
                Assert.Equal($"hg-{hub}.note", connectMessage.GroupName);

                var message = connectMessage.Payloads["json"]
                    .GetJsonMessageFromSingleFramePayload<HubResponseItem>();

                Assert.Equal("Connected", message.A[0]);

                // close transport layer
                proxy.TestConnectionContext.Application.Output.Complete();

                await connectionTask.OrTimeout();
                Assert.Equal(ServiceConnectionStatus.Disconnected, proxy.Status);

                // cleaned up clearly
                Assert.Empty(ccm.ClientConnections);
            }
        }
    }

    [Fact]
    public async Task ServiceConnectionWithTransportLayerClosedShouldCleanupEndlessConnectClientConnections()
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug, expectedErrors: c => true, logChecker:
            logs =>
            {
                var errorLogs = logs.Where(s => s.Write.LogLevel == LogLevel.Error).ToList();
                Assert.Single(errorLogs);
                Assert.Equal("ApplicationTaskTimedOut", errorLogs[0].Write.EventId.Name);

                return true;
            }))
        {
            var hubConfig = Utility.GetActualHubConfig(loggerFactory);
            var appName = "app1";
            var hub = "EndlessConnect";
            var scm = new TestServiceConnectionHandler();
            hubConfig.Resolver.Register(typeof(IServiceConnectionManager), () => scm);
            var ccm = new ClientConnectionManager(hubConfig, loggerFactory);
            hubConfig.Resolver.Register(typeof(IClientConnectionManager), () => ccm);
            DispatcherHelper.PrepareAndGetDispatcher(new TestAppBuilder(), hubConfig,
                new ServiceOptions { ConnectionString = ConnectionString }, appName, loggerFactory);
            using (var proxy = new TestServiceConnectionProxy(ccm, loggerFactory))
            {
                // start the server connection
                var connectionTask = proxy.StartAsync();
                await proxy.ConnectionInitializedTask.OrTimeout();

                var clientConnection = Guid.NewGuid().ToString("N");

                var connectTask = scm.WaitForTransportOutputMessageAsync(typeof(GroupBroadcastDataMessage))
                    .OrTimeout();
                // Application layer sends OpenConnectionMessage
                var openConnectionMessage = new OpenConnectionMessage(clientConnection, new Claim[0], null,
                    $"?transport=webSockets&connectionToken=conn1&connectionData=%5B%7B%22name%22%3A%22{hub}%22%7D%5D");
                await proxy.WriteMessageAsync(openConnectionMessage);

                var connectMessage = (await connectTask) as GroupBroadcastDataMessage;
                Assert.NotNull(connectMessage);
                Assert.Equal($"hg-{hub}.note", connectMessage.GroupName);

                var message = connectMessage.Payloads["json"]
                    .GetJsonMessageFromSingleFramePayload<HubResponseItem>();

                Assert.Equal("Connected", message.A[0]);

                // close transport layer
                proxy.TestConnectionContext.Application.Output.Complete();

                // wait for application task to timeout
                await proxy.WaitForConnectionClose.OrTimeout(10000);
                Assert.Equal(ServiceConnectionStatus.Disconnected, proxy.Status);

                // cleaned up clearly
                Assert.Empty(ccm.ClientConnections);
            }
        }
    }

    [Fact]
    public async Task ServiceConnectionWithTransportLayerClosedShouldCleanupEndlessInvokeClientConnections()
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug, expectedErrors: c => true))
        {
            var hubConfig = Utility.GetActualHubConfig(loggerFactory);
            var appName = "app1";
            var hub = "EndlessInvoke";
            var scm = new TestServiceConnectionHandler();
            hubConfig.Resolver.Register(typeof(IServiceConnectionManager), () => scm);
            var ccm = new ClientConnectionManager(hubConfig, loggerFactory);
            hubConfig.Resolver.Register(typeof(IClientConnectionManager), () => ccm);
            DispatcherHelper.PrepareAndGetDispatcher(new TestAppBuilder(), hubConfig,
                new ServiceOptions { ConnectionString = ConnectionString }, appName, loggerFactory);
            using (var proxy = new TestServiceConnectionProxy(ccm, loggerFactory))
            {
                // start the server connection
                var connectionTask = proxy.StartAsync();
                await proxy.ConnectionInitializedTask.OrTimeout();

                var clientConnection = Guid.NewGuid().ToString("N");

                var connectTask = scm.WaitForTransportOutputMessageAsync(typeof(GroupBroadcastDataMessage))
                    .OrTimeout();
                // Application layer sends OpenConnectionMessage
                var openConnectionMessage = new OpenConnectionMessage(clientConnection, new Claim[0], null,
                    $"?transport=webSockets&connectionToken=conn1&connectionData=%5B%7B%22name%22%3A%22{hub}%22%7D%5D");
                await proxy.WriteMessageAsync(openConnectionMessage);

                var connectMessage = (await connectTask) as GroupBroadcastDataMessage;
                Assert.NotNull(connectMessage);
                Assert.Equal($"hg-{hub}.note", connectMessage.GroupName);

                var message = connectMessage.Payloads["json"]
                    .GetJsonMessageFromSingleFramePayload<HubResponseItem>();

                Assert.Equal("Connected", message.A[0]);

                // group message goes into the manager
                // make sure the tcs is called before writing message
                var gbTask = scm.WaitForTransportOutputMessageAsync(typeof(GroupBroadcastDataMessage)).OrTimeout();

                await proxy.WriteMessageAsync(new ConnectionDataMessage(clientConnection, Encoding.UTF8.GetBytes($"{{\"H\":\"{hub}\",\"M\":\"JoinGroup\",\"A\":[\"user1\",\"group1\"],\"I\":1}}")));

                var broadcastMessage = (await gbTask) as GroupBroadcastDataMessage;
                Assert.NotNull(broadcastMessage);
                Assert.Equal($"hg-{hub}.group1", broadcastMessage.GroupName);

                // close transport layer
                proxy.TestConnectionContext.Application.Output.Complete();

                await connectionTask.OrTimeout();
                await proxy.WaitForConnectionClose.OrTimeout();
                Assert.Equal(ServiceConnectionStatus.Disconnected, proxy.Status);

                // cleaned up clearly
                Assert.Empty(ccm.ClientConnections);
            }
        }
    }

    [Fact]
    public async Task ServiceConnectionWithOfflinePingWillTriggerDisconnectClients()
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
        {
            var hubConfig = Utility.GetActualHubConfig(loggerFactory);
            var appName = "app1";
            var hub = "chat";
            var scm = new TestServiceConnectionHandler();
            hubConfig.Resolver.Register(typeof(IServiceConnectionManager), () => scm);
            var ccm = new ClientConnectionManager(hubConfig, loggerFactory);
            hubConfig.Resolver.Register(typeof(IClientConnectionManager), () => ccm);
            DispatcherHelper.PrepareAndGetDispatcher(new TestAppBuilder(), hubConfig, new ServiceOptions { ConnectionString = ConnectionString }, appName, loggerFactory);
            using (var proxy = new TestServiceConnectionProxy(ccm, loggerFactory: loggerFactory))
            {
                // prepare 2 clients with different instancesId connected
                var instanceId1 = Guid.NewGuid().ToString();
                var connectionId1 = Guid.NewGuid().ToString("N");
                var header1 = new Dictionary<string, StringValues>() { { Constants.AsrsInstanceId, instanceId1 } };
                var instanceId2 = Guid.NewGuid().ToString();
                var connectionId2 = Guid.NewGuid().ToString("N");
                var header2 = new Dictionary<string, StringValues>() { { Constants.AsrsInstanceId, instanceId2 } };

                // start the server connection
                await proxy.StartServiceAsync().OrTimeout();

                // Application layer sends OpenConnectionMessage for client1
                var connectTask = scm.WaitForTransportOutputMessageAsync(typeof(GroupBroadcastDataMessage)).OrTimeout();
                var openConnectionMessage = new OpenConnectionMessage(connectionId1, new Claim[0], header1, $"?transport=webSockets&connectionToken=conn1&connectionData=%5B%7B%22name%22%3A%22{hub}%22%7D%5D");
                await proxy.WriteMessageAsync(openConnectionMessage);

                // client1 is connected
                var connectMessage = (await connectTask) as GroupBroadcastDataMessage;
                Assert.NotNull(connectMessage);
                Assert.Equal($"hg-{hub}.note", connectMessage.GroupName);
                var message = connectMessage.Payloads["json"].GetJsonMessageFromSingleFramePayload<HubResponseItem>();
                Assert.Equal("Connected", message.A[0]);

                ccm.ClientConnections.TryGetValue(connectionId1, out var transport1);
                Assert.NotNull(transport1);

                // Application layer sends OpenConnectionMessage for client2
                connectTask = scm.WaitForTransportOutputMessageAsync(typeof(GroupBroadcastDataMessage)).OrTimeout();
                openConnectionMessage = new OpenConnectionMessage(connectionId2, new Claim[0], header2, $"?transport=webSockets&connectionToken=conn2&connectionData=%5B%7B%22name%22%3A%22{hub}%22%7D%5D");
                await proxy.WriteMessageAsync(openConnectionMessage);

                // client2 is connected
                connectMessage = (await connectTask) as GroupBroadcastDataMessage;
                Assert.NotNull(connectMessage);
                Assert.Equal($"hg-{hub}.note", connectMessage.GroupName);
                message = connectMessage.Payloads["json"].GetJsonMessageFromSingleFramePayload<HubResponseItem>();
                Assert.Equal("Connected", message.A[0]);
                ccm.ClientConnections.TryGetValue(connectionId2, out var transport2);
                Assert.NotNull(transport2);

                // Send ServerOfflinePing on instance1 and will trigger cleanup related client1
                connectTask = scm.WaitForTransportOutputMessageAsync(typeof(GroupBroadcastDataMessage)).OrTimeout();
                await proxy.WriteMessageAsync(new PingMessage()
                {
                    Messages = new[] { "offline", instanceId1 }
                });

                // Validate client1 disconnect 
                connectMessage = (await connectTask) as GroupBroadcastDataMessage;
                Assert.NotNull(connectMessage);
                Assert.Equal($"hg-{hub}.note", connectMessage.GroupName);
                message = connectMessage.Payloads["json"]
                    .GetJsonMessageFromSingleFramePayload<HubResponseItem>();
                Assert.Equal("Disconnected", message.A[0]);

                // Validate client2 is still connected
                Assert.Single(ccm.ClientConnections);
                Assert.Equal(connectionId2, ccm.ClientConnections.FirstOrDefault().Key);
            }
        }
    }

    private sealed class HubResponseItem
    {
        public string H { get; set; }
        public string M { get; set; }
        public List<string> A { get; set; }
    }
}
