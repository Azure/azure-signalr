// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class ClientConnectionContext
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public ClientConnectionContext(string connectionId, string instanceId = null)
        {
            ConnectionId = connectionId;
            InstanceId = instanceId;
            var channel = Channel.CreateUnbounded<ServiceMessage>();
            Input = channel.Reader;
            Output = channel.Writer;
        }

        public Task ApplicationTask { get; set; }

        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public void CancelPendingRead()
        {
            _cancellationTokenSource.Cancel();
        }

        public string ConnectionId { get; }

        public string InstanceId { get; }

        public ChannelReader<ServiceMessage> Input { get; }

        public ChannelWriter<ServiceMessage> Output { get; }

        public IServiceTransport Transport { get; set; }
    }
}
