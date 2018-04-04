// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Internal.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    // This class plays a different role from SignalR because it is not used in a real server:
    // No negotiation, no keep-alive ping sending. The connection context here is just
    // used to carry some basic information for LifetimeManager.
    // It is only a proxy class to fit in LifetimeManager and send message out.
    public class CloudHubConnectionContext : HubConnectionContext
    {
        private readonly CancellationTokenSource _connectionAbortedTokenSource = new CancellationTokenSource();
        private readonly TaskCompletionSource<object> _abortCompletedTcs = new TaskCompletionSource<object>();
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1);

        private IMessageSender _hubSender;

        // client's protocol obtained from HubMessageWrapper in OnConnected
        public IHubProtocol ClientProtocol { get; set; }

        // Currently used only for streaming methods
        internal ConcurrentDictionary<string, CancellationTokenSource> ActiveRequestCancellationSources { get; } = new
            ConcurrentDictionary<string, CancellationTokenSource>();

        public CloudHubConnectionContext(IHubProtocol hubProtocol, IMessageSender sender,
            ConnectionContext connectionContext, ILoggerFactory loggerFactory)
            : base(connectionContext, TimeSpan.FromSeconds(30), loggerFactory)
        {
            _hubSender = sender;
            ClientProtocol = hubProtocol;
        }

        // generate binary for both Json and MessagePack
        public Task SendAllProtocolRaw(IDictionary<string, string> meta, string method, object[] args)
        {
            return _hubSender.SendAllProtocolRawMessage(meta, method, args);
        }

        public Task SendRaw(IDictionary<string, string> meta, string method, object[] args)
        {
            var hubInvocationMessageWrapper = new HubInvocationMessageWrapper(ClientProtocol.TransferFormat);
            hubInvocationMessageWrapper.AddMetadata(meta);
            if (method != null)
            {
                var message = _hubSender.CreateInvocationMessage(method, args);
                hubInvocationMessageWrapper.WritePayload(ClientProtocol.TransferFormat, ClientProtocol.WriteToArray(message));
            }
            _ = _hubSender.SendHubMessage(hubInvocationMessageWrapper);
            return Task.CompletedTask;
        }

        public Task SendHubMessage(HubMessage hubMessage, IDictionary<string, string> meta)
        {
            var hubInvocationMessageWrapper = new HubInvocationMessageWrapper(ClientProtocol.TransferFormat);
            hubInvocationMessageWrapper.AddMetadata(meta);
            hubInvocationMessageWrapper.WritePayload(ClientProtocol.TransferFormat, ClientProtocol.WriteToArray(hubMessage));
            _ = _hubSender.SendHubMessage(hubInvocationMessageWrapper);
            return Task.CompletedTask;
        }

        public async override ValueTask WriteAsync(HubMessage message)
        {
            await _hubSender.SendHubMessage(message);
        }
    }
}
