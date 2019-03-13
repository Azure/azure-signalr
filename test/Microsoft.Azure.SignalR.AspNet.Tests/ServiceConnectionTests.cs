// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Transports;
using Microsoft.Azure.SignalR.AspNet.Tests.TestHubs;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.TestsCommon;
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

                var scm = new TestServiceConnectionHandler();
                hubConfig.Resolver.Register(typeof(IServiceConnectionManager), () => scm);
                var ccm = new ClientConnectionManager(hubConfig, loggerFactory);
                hubConfig.Resolver.Register(typeof(IClientConnectionManager), () => ccm);
                DispatcherHelper.PrepareAndGetDispatcher(new TestAppBuilder(), hubConfig, new ServiceOptions { ConnectionString = ConnectionString }, "app1", loggerFactory);
                using (var proxy = new TestServiceConnectionProxy(ccm, loggerFactory: loggerFactory))
                {
                    // start the server connection
                    await proxy.StartServiceAsync().OrTimeout();

                    var clientConnection = Guid.NewGuid().ToString("N");

                    // Application layer sends OpenConnectionMessage
                    var openConnectionMessage = new OpenConnectionMessage(clientConnection, new Claim[0], null, "?transport=webSockets&connectionToken=conn1");
                    await proxy.WriteMessageAsync(openConnectionMessage);
                    await proxy.WaitForClientConnectAsync(clientConnection).OrTimeout();

                    // group message goes into the manager
                    // make sure the tcs is called before writing message
                    var jgTask = scm.WaitForTransportOutputMessageAsync(typeof(JoinGroupMessage)).OrTimeout();

                    var gbTask = scm.WaitForTransportOutputMessageAsync(typeof(GroupBroadcastDataMessage)).OrTimeout();

                    await proxy.WriteMessageAsync(new ConnectionDataMessage(clientConnection, Encoding.UTF8.GetBytes("{\"H\":\"chat\",\"M\":\"JoinGroup\",\"A\":[\"user1\",\"message1\"],\"I\":1}")));
                    
                    await jgTask;
                    await gbTask;

                    var lgTask = scm.WaitForTransportOutputMessageAsync(typeof(LeaveGroupMessage)).OrTimeout();
                    gbTask = scm.WaitForTransportOutputMessageAsync(typeof(GroupBroadcastDataMessage)).OrTimeout();

                    await proxy.WriteMessageAsync(new ConnectionDataMessage(clientConnection, Encoding.UTF8.GetBytes("{\"H\":\"chat\",\"M\":\"LeaveGroup\",\"A\":[\"user1\",\"message1\"],\"I\":1}")));

                    await lgTask;
                    await gbTask;

                    var dTask = proxy.WaitForClientDisconnectAsync(clientConnection).OrTimeout();
                    await proxy.WriteMessageAsync(new CloseConnectionMessage(clientConnection));
                    await dTask;
                }
            }
        }
        
        [Fact]
        public async Task ServiceConnectionDispatchOpenConnectionToUnauthorizedHubTest()
        {
            bool ExpectedErrors(WriteContext writeContext)
            {
                return writeContext.LoggerName == typeof(ServiceConnection).FullName &&
                    writeContext.EventId == new EventId(11, "ConnectedStartingFailed") &&
                    writeContext.Exception.Message == "Unable to authorize request";
            }
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug, expectedErrorsMatch: ExpectedErrors))
            {
                var hubConfig = new HubConfiguration();
                var ccm = new ClientConnectionManager(hubConfig);
                hubConfig.Resolver.Register(typeof(IClientConnectionManager), () => ccm);
                using (var proxy = new ServiceConnectionProxy(ccm, loggerFactory: loggerFactory))
                {
                    // start the server connection
                    await proxy.StartServiceAsync().OrTimeout();

                    var clientConnection = Guid.NewGuid().ToString("N");

                    // Application layer sends OpenConnectionMessage to an authorized hub from anonymous user
                    var openConnectionMessage = new OpenConnectionMessage(clientConnection, new Claim[0], null, "?transport=webSockets&connectionData=%5B%7B%22name%22%3A%22authchat%22%7D%5D");
                    await proxy.WriteMessageAsync(openConnectionMessage);
                    await proxy.WaitForClientConnectAsync(clientConnection).OrTimeout();
                    
                    // Verify client connection is not created due to authorized failure.
                    ccm.TryGetServiceConnection(clientConnection, out var serviceConnection);
                    Assert.Null(serviceConnection);
                }
            }
        }
    }
}
