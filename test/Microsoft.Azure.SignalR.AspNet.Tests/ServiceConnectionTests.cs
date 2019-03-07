// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Transports;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.TestsCommon;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    public partial class ServiceConnectionTests : VerifiableLoggedTest
    {
        private const string ConnectionString = "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;";

        private readonly TestClientConnectionManager _clientConnectionManager;

        public ServiceConnectionTests(ITestOutputHelper output) : base(output)
        {
            var hubConfig = new HubConfiguration();
            var transport = new AzureTransportManager(hubConfig.Resolver);
            hubConfig.Resolver.Register(typeof(ITransportManager), () => transport);

            _clientConnectionManager = new TestClientConnectionManager();
        }

        [Fact]
        public async Task ServiceConnectionDispatchTest()
        {
            int count = 0;
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {
                using (var proxy = new TestServiceConnectionProxy(_clientConnectionManager, loggerFactory: loggerFactory))
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
                    _clientConnectionManager.CurrentTransports.TryGetValue(clientConnection, out var transport);
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
                var hubConfig = new HubConfiguration();
                hubConfig.Resolver = new DefaultDependencyResolver();
                var scm = new TestServiceConnectionHandler();
                hubConfig.Resolver.Register(typeof(IServiceConnectionManager), () => scm);

                var ccm = new ClientConnectionManager(hubConfig);
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
    }
}
