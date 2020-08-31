// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;
using System;
using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Threading.Tasks;


namespace Microsoft.Azure.SignalR.IntegrationTests.MockService
{
    /// <summary>
    /// Represents client connection on mock service side
    /// Allows sending messages to and storing received messages from SDK
    /// </summary>
    internal class MockServiceSideClientConnection
    {
        private static readonly JsonHubProtocol _signalRPro = new JsonHubProtocol();
        private static readonly ServiceProtocol _servicePro = new ServiceProtocol();


        public string ConnectionId { get; private set; }
        public MockServiceSideConnection ServiceSideConnection { get; private set; }

        public int Index { get; set; }
        public bool ExpectsClientHandshake { get; set; } = true;
        public TaskCompletionSource<string> HandshakeCompleted { get; } = new TaskCompletionSource<string>();
        public bool CloseMessageReceivedFromSdk { get; set; }


        int _invId = 0;

        public async Task SendMessage(string target, object[] args)
        {
            var callHubRequest = new InvocationMessage(invocationId: _invId++.ToString(), target: target, arguments: args);
            var callHubServiceMessage = new ConnectionDataMessage(ConnectionId, _signalRPro.GetMessageBytes(callHubRequest));
            _servicePro.WriteMessage(callHubServiceMessage, ServiceSideConnection.MockServicePipe.Output);
            var flushResult = await ServiceSideConnection.MockServicePipe.Output.FlushAsync();

            if (flushResult.IsCanceled || flushResult.IsCompleted)
            {
                throw new InvalidOperationException($"Sending InvocationMessage {_invId} for client connection id {ConnectionId} resulted in FlushResult with IsCanceled {flushResult.IsCanceled} IsCompleted {flushResult.IsCompleted}");
            }
        }

        public async Task CloseConnection()
        {
            var closeClientMessage = new CloseConnectionMessage(ConnectionId, "bbb");
            _servicePro.WriteMessage(closeClientMessage, ServiceSideConnection.MockServicePipe.Output);
            var flushResult = await ServiceSideConnection.MockServicePipe.Output.FlushAsync();

            if (flushResult.IsCanceled || flushResult.IsCompleted)
            {
                throw new InvalidOperationException($"CloseConnectionMessage for client connection id {ConnectionId} resulted in FlushResult IsCanceled {flushResult.IsCanceled} IsCompleted {flushResult.IsCompleted}");
            }
        }

        public MockServiceSideClientConnection(string connectionId, MockServiceSideConnection serviceSideConnection)
        {
            ConnectionId = connectionId;
            ServiceSideConnection = serviceSideConnection;
        }

        private ConcurrentDictionary<Type, Channel<HubMessage>> _hubMessagesFromSDK = new ConcurrentDictionary<Type, Channel<HubMessage>>();

        public void EnqueueMessage(HubMessage m) =>
            _hubMessagesFromSDK.GetOrAdd(m.GetType(), _ => CreateChannel<HubMessage>()).Writer.TryWrite(m);

        public async Task<TServiceMessage> DequeueMessageAsync<TServiceMessage>() where TServiceMessage : HubMessage =>
            await _hubMessagesFromSDK.GetOrAdd(typeof(TServiceMessage), _ => CreateChannel<HubMessage>()).Reader.ReadAsync() as TServiceMessage;

        public ValueTask<bool> WaitToDequeueMessageAsync<TServiceMessage>() where TServiceMessage : ServiceMessage =>
            _hubMessagesFromSDK.GetOrAdd(typeof(TServiceMessage), _ => CreateChannel<HubMessage>()).Reader.WaitToReadAsync();

        private static Channel<T> CreateChannel<T>() => Channel.CreateUnbounded<T>(
                new UnboundedChannelOptions() { SingleReader = true, SingleWriter = true, AllowSynchronousContinuations = false });
    }
}
