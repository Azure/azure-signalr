// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Security.Claims;
using System.Threading.Channels;
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
            var protectedData = new EmptyProtectedData();
            var transport = new AzureTransportManager(hubConfig.Resolver);
            hubConfig.Resolver.Register(typeof(IProtectedData), () => protectedData);
            hubConfig.Resolver.Register(typeof(ITransportManager), () => transport);

            _clientConnectionManager = new ClientConnectionManager(hubConfig);
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

                    await ReadSingleServiceMessageAsync<HandshakeResponseMessage>(proxy.ConnectionContext.Input);

                    // Application layer sends OpenConnectionMessage
                    var openConnectionMessage = new OpenConnectionMessage(clientConnection, new Claim[0], null, "?transport=webSockets");
                    await proxy.WriteMessageAsync(new OpenConnectionMessage(clientConnection, new Claim[0]));
                    await proxy.WaitForClientConnectAsync(clientConnection).OrTimeout();
                    await ReadSingleServiceMessageAsync<OpenConnectionMessage>(proxy.ConnectionContext.Input);

                    // TODO: Check response when integrated with ServiceMessageBus
                    while(count < 1000)
                    {
                        await proxy.WriteMessageAsync(new ConnectionDataMessage(clientConnection, GetPayload("Hello World")));
                        await proxy.WaitForApplicationMessageAsync(clientConnection).OrTimeout();
                        count++;
                    }
                    while(count > 0)
                    {
                        await ReadSingleServiceMessageAsync<ConnectionDataMessage>(proxy.ConnectionContext.Input);
                        count--;
                    }

                    await proxy.WriteMessageAsync(new CloseConnectionMessage(clientConnection));
                    await proxy.WaitForClientDisconnectAsync(clientConnection).OrTimeout();
                    await ReadSingleServiceMessageAsync<CloseConnectionMessage>(proxy.ConnectionContext.Input);
                }
            }
        }

        private ReadOnlyMemory<byte> GetPayload(string message)
        {
            return Protocol.GetMessageBytes(new ConnectionDataMessage(string.Empty, System.Text.Encoding.UTF8.GetBytes(message)));
        }

        private static async Task<T> ReadSingleServiceMessageAsync<T>(ChannelReader<ServiceMessage> input, int timeout = 5000) 
            where T: ServiceMessage
        {
            ServiceMessage message = null;

            while(await input.WaitToReadAsync())
            {
                if(input.TryRead(out message))
                {
                    return Assert.IsType<T>(message);
                }
            }
            return Assert.IsType<T>(message);
        }
    }
}
