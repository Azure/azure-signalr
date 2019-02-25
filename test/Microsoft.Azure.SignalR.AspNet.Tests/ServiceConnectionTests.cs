// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Transports;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Owin;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    public partial class ServiceConnectionTests : VerifiableLoggedTest
    {
        private const string ConnectionString = "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;";
        private static readonly ServiceProtocol Protocol = new ServiceProtocol();

        private readonly TestConnectionManager _clientConnectionManager;

        public ServiceConnectionTests(ITestOutputHelper output) : base(output)
        {
            var hubConfig = new HubConfiguration();
            var transport = new AzureTransportManager(hubConfig.Resolver);
            hubConfig.Resolver.Register(typeof(ITransportManager), () => transport);

            _clientConnectionManager = new TestConnectionManager();
        }

        [Fact]
        public async Task ServiceConnectionDispatchTest()
        {
            int count = 0;
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {
                using (var proxy = new ServiceConnectionProxy(_clientConnectionManager, loggerFactory: loggerFactory))
                {
                    // start the server connection
                    await proxy.StartServiceAsync().OrTimeout();

                    var clientConnection = Guid.NewGuid().ToString("N");

                    // Application layer sends OpenConnectionMessage
                    var openConnectionMessage = new OpenConnectionMessage(clientConnection, new Claim[0], null, "?transport=webSockets");
                    await proxy.WriteMessageAsync(openConnectionMessage);
                    await proxy.WaitForClientConnectAsync(clientConnection).OrTimeout();
                    
                    while (count < 1000)
                    {
                        await proxy.WriteMessageAsync(new ConnectionDataMessage(clientConnection, GetPayload("Hello World")));
                        await proxy.WaitForApplicationMessageAsync(clientConnection).OrTimeout();
                        count++;
                    }

                    await proxy.WriteMessageAsync(new CloseConnectionMessage(clientConnection));
                    await proxy.WaitForClientDisconnectAsync(clientConnection).OrTimeout();

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
                var scm = new TestServiceConnectionManager();
                hubConfig.Resolver.Register(typeof(IServiceConnectionManager), () => scm);

                var ccm = new ClientConnectionManager(hubConfig);
                hubConfig.Resolver.Register(typeof(IClientConnectionManager), () => ccm);

                DispatcherHelper.PrepareAndGetDispatcher(new TestAppBuilder(), hubConfig, new ServiceOptions { ConnectionString = ConnectionString }, "app1", loggerFactory);

                using (var proxy = new ServiceConnectionProxy(ccm, loggerFactory: loggerFactory))
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

                    await proxy.WriteMessageAsync(new CloseConnectionMessage(clientConnection));
                    await proxy.WaitForClientDisconnectAsync(clientConnection).OrTimeout();
                }
            }
        }

        private sealed class TestAppBuilder : IAppBuilder
        {
            public IDictionary<string, object> Properties { get; } = new Dictionary<string, object>()
            {
                ["builder.AddSignatureConversion"] = new Action<Delegate>(e => { })
            };

            public object Build(Type returnType)
            {
                return null;
            }

            public IAppBuilder New()
            {
                return null;
            }

            public IAppBuilder Use(object middleware, params object[] args)
            {
                return this;
            }
        }

        private ReadOnlyMemory<byte> GetPayload(string message)
        {
            return Protocol.GetMessageBytes(new ConnectionDataMessage(string.Empty, System.Text.Encoding.UTF8.GetBytes(message)));
        }
    }
}
