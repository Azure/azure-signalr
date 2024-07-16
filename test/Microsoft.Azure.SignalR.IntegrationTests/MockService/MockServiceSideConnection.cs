// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR;
using Microsoft.Azure.SignalR.IntegrationTests.Infrastructure;
using Microsoft.Azure.SignalR.Protocol;
using HandshakeRequestMessage = Microsoft.Azure.SignalR.Protocol.HandshakeRequestMessage;
using HandshakeResponseMessage = Microsoft.Azure.SignalR.Protocol.HandshakeResponseMessage;
using ServicePingMessage = Microsoft.Azure.SignalR.Protocol.PingMessage;

namespace Microsoft.Azure.SignalR.IntegrationTests.MockService
{
    /// <summary>
    /// Represents service connection on mock service side.
    /// Provides start / stop / connect new client functionality 
    /// Receives and stores messages received from SDK side
    /// </summary>
    internal class MockServiceSideConnection : IAsyncDisposable
    {
        private static readonly ServiceProtocol _servicePro = new ServiceProtocol();
        private static readonly JsonHubProtocol _signalRPro = new JsonHubProtocol();
        private static int s_clientConnNum = 0;

        private static int s_index = 0;
        private Task _processIncoming;
        private TaskCompletionSource<bool> _completedHandshake = new TaskCompletionSource<bool>();
        private ConcurrentDictionary<Type, Channel<ServiceMessage>> _messagesFromSDK = new ConcurrentDictionary<Type, Channel<ServiceMessage>>();
        private int _stopped = 0;
        
        // to help with debugging, make public if useful to check in tests
        private Exception _processIncomingException = null;
        private FlushResult _lastFlushResult;
        private ReadResult _lastReadResult;

        public MockServiceSideConnection(IMockService mocksvc, MockServiceConnectionContext sdkSideConnCtx, HubServiceEndpoint endpoint, string target, IDuplexPipe pipe)
        {
            Index = Interlocked.Increment(ref s_index);
            MockSvc = mocksvc;
            SDKSideServiceConnection = sdkSideConnCtx;
            Endpoint = endpoint;
            Target = target;
            MockServicePipe = pipe;
        }

        public Task ProcessIncoming => _processIncoming;
        public int Index { get; private set; }
        public IMockService MockSvc { get; private set; }
        public MockServiceConnectionContext SDKSideServiceConnection { get; private set; }
        public HubServiceEndpoint Endpoint { get; private set; }
        public string Target { get; private set; }
        public IDuplexPipe MockServicePipe { get; private set; }

        public List<MockServiceSideClientConnection> ClientConnections { get; } = new List<MockServiceSideClientConnection>();

        public async Task<MockServiceSideClientConnection> ConnectClientAsync()
        {
            if (!await _completedHandshake.Task)
            {
                throw new InvalidOperationException("Service connection has failed service connection handshake");
            }

            var clientConnId = SDKSideServiceConnection.ConnectionId + "_client_" + Interlocked.Increment(ref s_clientConnNum);
            MockServiceSideClientConnection clientConn = new MockServiceSideClientConnection(clientConnId, this);
            ClientConnections.Add(clientConn);

            var openClientConnMsg = new OpenConnectionMessage(clientConnId, new System.Security.Claims.Claim[] { }) { Protocol = "json" };
            _servicePro.WriteMessage(openClientConnMsg, MockServicePipe.Output);
            var flushResult = _lastFlushResult = await MockServicePipe.Output.FlushAsync();

            if (flushResult.IsCanceled || flushResult.IsCompleted)
            {
                // any better way?
                throw new InvalidOperationException($"Sending OpenConnectionMessage for clientConnId {clientConnId} returned flush result: IsCanceled {flushResult.IsCanceled} IsCompleted {flushResult.IsCompleted}");
            }


            var clientHandshakeRequest = new AspNetCore.SignalR.Protocol.HandshakeRequestMessage("json", 1);
            var clientHandshake = new ConnectionDataMessage(clientConnId, GetMessageBytes(clientHandshakeRequest));
            _servicePro.WriteMessage(clientHandshake, MockServicePipe.Output);
            flushResult = _lastFlushResult = await MockServicePipe.Output.FlushAsync();

            if (flushResult.IsCanceled || flushResult.IsCompleted)
            {
                throw new InvalidOperationException($"Sending HandshakeRequestMessage for clientConnId {clientConnId} returned flush result: IsCanceled {flushResult.IsCanceled} IsCompleted {flushResult.IsCompleted}");
            }

            string hsErr = await clientConn.HandshakeCompleted.Task;
            if (!string.IsNullOrEmpty(hsErr))
            {
                throw new InvalidOperationException($"client connection {clientConnId} handshake returned {hsErr}");
            }

            return clientConn;
        }

        public void Start()
        {
            _processIncoming = Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        var result = _lastReadResult = await MockServicePipe.Input.ReadAsync();
                        if (result.IsCanceled || result.IsCompleted)
                        {
                            break;
                        }

                        var buffer = result.Buffer;

                        try
                        {
                            if (!buffer.IsEmpty)
                            {
                                while (_servicePro.TryParseMessage(ref buffer, out var message))
                                {
                                    // always enqueue so tests can peek and analyze any of these messages
                                    EnqueueMessage(message);

                                    // now react to some of the connection related stuff
                                    if (message is HandshakeRequestMessage)
                                    {
                                        var handshakeResponse = new HandshakeResponseMessage("");
                                        _servicePro.WriteMessage(handshakeResponse, MockServicePipe.Output);
                                        var flushResult = _lastFlushResult = await MockServicePipe.Output.FlushAsync();

                                        if (flushResult.IsCanceled || flushResult.IsCompleted)
                                        {
                                            _completedHandshake.TrySetResult(false);
                                        }
                                        else
                                        {
                                            // sending ack merely allows SDK side to proceed with establishing the connection
                                            // for this service connection to become available for hubs to send messages 
                                            // we'd need to wait for SDK side service connection to change its status
                                            _completedHandshake.TrySetResult(true);
                                        }
                                        continue;
                                    }
                                    else if (message is ConnectionDataMessage cdm)
                                    {
                                        var payload = cdm.Payload;

                                        // do we know this client?
                                        var clientConnection = ClientConnections.Where(c => c.ConnectionId == cdm.ConnectionId).FirstOrDefault();
                                        if (clientConnection != null)
                                        {
                                            // is this client expecting handshake response?
                                            if (clientConnection.ExpectsClientHandshake)
                                            {
                                                // todo: maybe try parse first and then check if handshake is expected?
                                                if (HandshakeProtocol.TryParseResponseMessage(ref payload, out var response))
                                                {
                                                    clientConnection.ExpectsClientHandshake = false;
                                                    clientConnection.HandshakeCompleted.TrySetResult(response.Error);
                                                }
                                            }

                                            // There is no such goal to provide full message parsing capabilities here
                                            // But it is useful to know the hub invocation return result in some tests so there we have it.
                                            while (_signalRPro.TryParseMessage(ref payload, MockSvc.CurrentInvocationBinder, out HubMessage hubMessage))
                                            {
                                                clientConnection.EnqueueMessage(hubMessage);

                                                if (hubMessage is CloseMessage closeMsg)
                                                {
                                                    clientConnection.CloseMessageReceivedFromSdk = true;
                                                }
                                            }
                                        }
                                    }
                                    else if (message is ServicePingMessage ping && ping.IsFin())
                                    {
                                        var pong = RuntimeServicePingMessage.GetFinAckPingMessage();
                                        _servicePro.WriteMessage(pong, MockServicePipe.Output);
                                        var flushResult = _lastFlushResult = await MockServicePipe.Output.FlushAsync();

                                        //todo: do we care about this flush result?
                                    }
                                }
                            }
                        }
                        finally
                        {
                            MockServicePipe.Input.AdvanceTo(buffer.Start, buffer.End);
                        }
                    }
                }
                catch (Exception e)
                {
                    _processIncomingException = e;
                }
            });
        }

        // Note: this is just one way of closing the connection
        // Todo: add more Stop* methods (e.g. send ServiceErrorMessage, etc)
        public async Task StopAsync()
        {
            // ignore extra calls to stop
            if (Interlocked.CompareExchange(ref _stopped, 1, 0) == 0)
            {
                MockServicePipe.Output.Complete();
                MockServicePipe.Input.CancelPendingRead();
                await _processIncoming;

                MockSvc.UnregisterMockServiceSideConnection(this);
            }
        }

        private void EnqueueMessage(ServiceMessage m) =>
            _messagesFromSDK.GetOrAdd(m.GetType(), _ => CreateChannel<ServiceMessage>()).Writer.TryWrite(m);

        public async Task<TServiceMessage> DequeueMessageAsync<TServiceMessage>() where TServiceMessage : ServiceMessage =>
            await _messagesFromSDK.GetOrAdd(typeof(TServiceMessage), _ => CreateChannel<ServiceMessage>()).Reader.ReadAsync() as TServiceMessage;

        public ValueTask<bool> WaitToDequeueMessageAsync<TServiceMessage>() where TServiceMessage : ServiceMessage =>
            _messagesFromSDK.GetOrAdd(typeof(TServiceMessage), _ => CreateChannel<ServiceMessage>()).Reader.WaitToReadAsync();

        public static ReadOnlyMemory<byte> GetMessageBytes(Microsoft.AspNetCore.SignalR.Protocol.HandshakeRequestMessage message)
        {
            var writer = MemoryBufferWriter.Get();
            try
            {
                Microsoft.AspNetCore.SignalR.Protocol.HandshakeProtocol.WriteRequestMessage(message, writer);
                return writer.ToArray();
            }
            finally
            {
                MemoryBufferWriter.Return(writer);
            }
        }

        private static Channel<T> CreateChannel<T>() => Channel.CreateUnbounded<T>(
            new UnboundedChannelOptions() { SingleReader = true, SingleWriter = true, AllowSynchronousContinuations = false });

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
        }
    }
}
