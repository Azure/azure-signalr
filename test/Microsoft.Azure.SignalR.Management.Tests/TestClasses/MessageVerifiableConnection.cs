// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    internal class MessageVerifiableConnection : TestServiceConnection
    {
        public ConcurrentQueue<ServiceMessage> ReceivedMessages { get; } = new();

        public MessageVerifiableConnection(ServiceConnectionStatus status = ServiceConnectionStatus.Connected,
            ILogger logger = null,
            IServiceMessageHandler serviceMessageHandler = null,
            IServiceEventHandler serviceEventHandler = null) : base(status, false, logger, serviceMessageHandler, serviceEventHandler)
        {
        }

        protected override Task<bool> SafeWriteAsync(ServiceMessage serviceMessage)
        {
            ReceivedMessages.Enqueue(serviceMessage);
            return Task.FromResult(true);
        }
    }
}