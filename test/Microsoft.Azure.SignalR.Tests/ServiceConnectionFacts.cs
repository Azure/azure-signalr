// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;
using Xunit;
using HandshakeRequestMessage = Microsoft.Azure.SignalR.Protocol.HandshakeRequestMessage;
using HandshakeResponseMessage = Microsoft.Azure.SignalR.Protocol.HandshakeResponseMessage;

namespace Microsoft.Azure.SignalR.Tests
{
    public class ServiceConnectionFacts
    {
        private static readonly TimeSpan DefaultHandshakeTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan DefaultDelay = TimeSpan.FromMilliseconds(100);
        private static readonly IServiceProtocol ServiceProtocol = new ServiceProtocol();

        [Theory]
        [InlineData("json")]
        [InlineData("messagepack")]
        public async Task ServiceConnectionStartsConnection(string hubProtocolName)
        {
            var connectionId1 = Guid.NewGuid().ToString("N");
            var connectionId2 = Guid.NewGuid().ToString("N");

            var proxy = new ServiceConnectionProxy(hubProtocolName);

            proxy.Start();

            await HandshakeAsync(proxy.ConnectionContext);

            Assert.Empty(proxy.ClientConnectionManager.ClientConnections);

            // Create a new client connection
            await WriteMessageAsync(proxy.ConnectionContext, new OpenConnectionMessage(connectionId1, null));
            await Task.Delay(DefaultDelay);

            Assert.Single(proxy.ClientConnectionManager.ClientConnections);

            // Create another client connection
            await WriteMessageAsync(proxy.ConnectionContext, new OpenConnectionMessage(connectionId2, null));
            await Task.Delay(DefaultDelay);
            Assert.Equal(2, proxy.ClientConnectionManager.ClientConnections.Count);

            // Send a message to client 1
            Assert.Equal(0, proxy.ConnectionMessageCounter[connectionId1]);
            Assert.Equal(0, proxy.ConnectionMessageCounter[connectionId2]);

            var hubMessageBytes = proxy.GetHubMessageBytes();
            await WriteMessageAsync(proxy.ConnectionContext, new ConnectionDataMessage(connectionId1, hubMessageBytes));
            await Task.Delay(DefaultDelay);

            Assert.Equal(1, proxy.ConnectionMessageCounter[connectionId1]);
            Assert.Equal(0, proxy.ConnectionMessageCounter[connectionId2]);

            // Close client 1
            await WriteMessageAsync(proxy.ConnectionContext, new CloseConnectionMessage(connectionId1, null));
            await Task.Delay(DefaultDelay);

            Assert.Single(proxy.ClientConnectionManager.ClientConnections);

            // Close client 2
            await WriteMessageAsync(proxy.ConnectionContext, new CloseConnectionMessage(connectionId2, null));
            await Task.Delay(DefaultDelay);

            Assert.Empty(proxy.ClientConnectionManager.ClientConnections);

            proxy.Stop();
        }

        private async Task HandshakeAsync(TestConnection connection)
        {
            using (var handshakeCts = new CancellationTokenSource(DefaultHandshakeTimeout))
            {
                await ReceiveHandshakeRequestAsync(connection.Application.Input, handshakeCts.Token);
            }

            await WriteMessageAsync(connection, new HandshakeResponseMessage());
        }

        private async Task ReceiveHandshakeRequestAsync(PipeReader input, CancellationToken cancellationToken)
        {
            while (true)
            {
                var result = await input.ReadAsync(cancellationToken);

                var buffer = result.Buffer;
                var consumed = buffer.Start;
                var examined = buffer.End;

                try
                {
                    if (!buffer.IsEmpty)
                    {
                        if (ServiceProtocol.TryParseMessage(ref buffer, out var message))
                        {
                            consumed = buffer.Start;
                            examined = consumed;

                            if (!(message is HandshakeRequestMessage handshakeRequest))
                            {
                                throw new InvalidDataException(
                                    $"{message.GetType().Name} received when waiting for handshake request.");
                            }

                            if (handshakeRequest.Version != ServiceProtocol.Version)
                            {
                                throw new InvalidDataException("Protocol version not supported.");
                            }

                            break;
                        }
                    }

                    if (result.IsCompleted)
                    {
                        // Not enough data, and we won't be getting any more data.
                        throw new InvalidOperationException(
                            "Service connectioned disconnected before sending a handshake request");
                    }
                }
                finally
                {
                    input.AdvanceTo(consumed, examined);
                }
            }
        }

        private async Task WriteMessageAsync(TestConnection connection, ServiceMessage message)
        {
            ServiceProtocol.WriteMessage(message, connection.Application.Output);
            await connection.Application.Output.FlushAsync();
        }
    }
}
