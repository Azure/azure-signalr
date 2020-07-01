// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipelines;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.IntegrationTests.MockService
{
    // This is just a mock of the ServiceMock (overly simplistic to shorten the current PR)
    public class MockServiceMock : IMockService
    {
        public TaskCompletionSource<bool> CompletedServiceConnectionHandshake { get; } = new TaskCompletionSource<bool>();
        public IDuplexPipe MockServicePipe { get; set; }

        Task _processIncoming;

        public Task StartAsync()
        {
            var servicePro = new ServiceProtocol();

            _processIncoming = Task.Run(async () =>
            {
                while (true)
                {
                    var result = await MockServicePipe.Input.ReadAsync();
                    if (result.IsCanceled || result.IsCompleted)
                    {
                        break;
                    }

                    var buffer = result.Buffer;

                    try
                    {
                        if (!buffer.IsEmpty)
                        {
                            while (servicePro.TryParseMessage(ref buffer, out var message))
                            {
                                if (message is HandshakeRequestMessage)
                                {
                                    var handshakeResponse = new HandshakeResponseMessage("");
                                    servicePro.WriteMessage(handshakeResponse, MockServicePipe.Output);
                                    var flushResult = await MockServicePipe.Output.FlushAsync();
                                    if (flushResult.IsCanceled || flushResult.IsCompleted)
                                    {
                                        CompletedServiceConnectionHandshake.TrySetResult(false);
                                    }
                                    else
                                    {
                                        CompletedServiceConnectionHandshake.TrySetResult(true);
                                    }
                                    break;
                                }
                            }
                        }
                    }
                    finally
                    {
                        MockServicePipe.Input.AdvanceTo(buffer.Start, buffer.End);
                    }
                }
            });
            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            MockServicePipe.Output.Complete();
            MockServicePipe.Input.CancelPendingRead();
            await _processIncoming;
        }
    }
}