// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.AspNet.SignalR.Transports;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    public partial class ServiceConnectionTests : VerifiableLoggedTest
    {
        private static readonly ServiceProtocol Protocol = new ServiceProtocol();

        private readonly ClientConnectionManager _clientConnectionManager;

        public ServiceConnectionTests(ITestOutputHelper output) : base(output)
        {
            var hubConfig = new HubConfiguration();
            var dispatcher = new ServiceHubDispatcher(hubConfig);
            var protectedData = new EmptyProtectedData();
            var transport = new AzureTransportManager(hubConfig.Resolver);
            hubConfig.Resolver.Register(typeof(PersistentConnection), () => dispatcher);
            hubConfig.Resolver.Register(typeof(IProtectedData), () => protectedData);
            hubConfig.Resolver.Register(typeof(ITransportManager), () => transport);

            dispatcher.Initialize(hubConfig.Resolver);
            _clientConnectionManager = new ClientConnectionManager(hubConfig.Resolver);
        }

        [Fact]
        public async Task ServiceConnectionDispatchTest()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {
                using (var proxy = new ServiceConnectionProxy(_clientConnectionManager, loggerFactory: loggerFactory))
                {
                    // start the server connection
                    await proxy.StartServiceAsync().OrTimeout();

                    var clientConnection = Guid.NewGuid().ToString("N");

                    // Application layer sends OpenConnectionMessage
                    var openConnectionMessage = new OpenConnectionMessage(clientConnection, new Claim[0], null, "?transport=webSockets");
                    await proxy.WriteMessageAsync(new OpenConnectionMessage(clientConnection, new Claim[0]));

                    await proxy.WaitForClientConnectAsync(clientConnection).OrTimeout();

                    // TODO: Check response when integrated with ServiceMessageBus
                    await proxy.WriteMessageAsync(new ConnectionDataMessage(clientConnection, GetPayload("Hello World")));

                    await proxy.WaitForApplicationMessageAsync(clientConnection).OrTimeout();

                    await proxy.WriteMessageAsync(new CloseConnectionMessage(clientConnection));

                    await proxy.WaitForClientDisconnectAsync(clientConnection).OrTimeout();
                }
            }
        }

        private ReadOnlyMemory<byte> GetPayload(string message)
        {
            return Protocol.GetMessageBytes(new ConnectionDataMessage(string.Empty, System.Text.Encoding.UTF8.GetBytes(message)));
        }
    }
}
