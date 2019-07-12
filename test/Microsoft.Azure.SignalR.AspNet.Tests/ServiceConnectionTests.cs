// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Web.WebSockets;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Transports;
using Microsoft.Azure.SignalR.AspNet.Tests.TestHubs;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    public partial class ServiceConnectionTests : VerifiableLoggedTest
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
                    var task = proxy.WaitForClientConnectAsync(clientConnection).OrTimeout();
                    await proxy.WriteMessageAsync(openConnectionMessage);
                    await task;
                    
                    while (count < 1000)
                    {
                        task = proxy.WaitForApplicationMessageAsync(clientConnection).OrTimeout();
                        await proxy.WriteMessageAsync(new ConnectionDataMessage(clientConnection, "Hello World".GenerateSingleFrameBuffer()));
                        await task;
                        count++;
                    }

                    task = proxy.WaitForClientDisconnectAsync(clientConnection).OrTimeout();
                    await proxy.WriteMessageAsync(new CloseConnectionMessage(clientConnection));
                    await task;

                    // Validate in transport for 1000 data messages.
                    clientConnectionManager.CurrentTransports.TryGetValue(clientConnection, out var transport);
                    Assert.NotNull(transport);
                    await transport.WaitOnDisconnected().OrTimeout();
                    Assert.Equal(transport.MessageCount, count);
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
                    await proxy.WaitForClientConnectAsync(clientConnection).OrTimeout();

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
                    Assert.Single(logs);
                    Assert.Equal("ConnectedStartingFailed", logs[0].Write.EventId.Name);
                    return true;
                }))
            {
                var hubConfig = Utility.GetActualHubConfig(loggerFactory);
                var appName = "app1";
                var hub = "ec"; // error connect hub
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
                    await proxy.WaitForClientConnectAsync(clientConnection).OrTimeout();

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

                    var clientConnection = Guid.NewGuid().ToString("N");
                    var connectTask = proxy.WaitForOutgoingMessageAsync(clientConnection).OrTimeout();

                    // Application layer sends OpenConnectionMessage to an authorized hub from anonymous user
                    var openConnectionMessage = new OpenConnectionMessage(clientConnection, new Claim[0], null, "?transport=webSockets&connectionData=%5B%7B%22name%22%3A%22authchat%22%7D%5D");
                    await proxy.WriteMessageAsync(openConnectionMessage);
                    await proxy.WaitForClientConnectAsync(clientConnection).OrTimeout();

                    var message = await connectTask;

                    Assert.True(message is CloseConnectionMessage);

                    // Verify client connection is not created due to authorized failure.
                    ccm.TryGetServiceConnection(clientConnection, out var serviceConnection);
                    Assert.Null(serviceConnection);
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

                    var clientConnection = Guid.NewGuid().ToString("N");

                    var connectTask = proxy.WaitForOutgoingMessageAsync(clientConnection).OrTimeout();

                    // Application layer sends OpenConnectionMessage to an authorized hub from anonymous user
                    var openConnectionMessage = new OpenConnectionMessage(clientConnection, new Claim[0], null, "?transport=webSockets&connectionData=%5B%7B%22name%22%3A%22authchat%22%7D%5D");
                    await proxy.WriteMessageAsync(openConnectionMessage);
                    await proxy.WaitForClientConnectAsync(clientConnection).OrTimeout();

                    var message = await connectTask;

                    Assert.True(message is CloseConnectionMessage);

                    // Verify client connection is not created due to authorized failure.
                    ccm.TryGetServiceConnection(clientConnection, out var serviceConnection);
                    Assert.Null(serviceConnection);
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
}
