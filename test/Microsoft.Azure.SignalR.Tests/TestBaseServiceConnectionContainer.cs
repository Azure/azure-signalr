// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.SignalR.Tests
{
    internal sealed class TestBaseServiceConnectionContainer : ServiceConnectionContainerBase
    {
        public TestBaseServiceConnectionContainer(List<IServiceConnection> serviceConnections, HubServiceEndpoint endpoint = null, AckHandler ackHandler = null)
            : base(null, 0, endpoint, serviceConnections, ackHandler: ackHandler, logger: NullLogger.Instance)
        {
        }

        public override Task HandlePingAsync(PingMessage pingMessage)
        {
            return Task.CompletedTask;
        }

        protected override Task OnConnectionComplete(IServiceConnection connection)
        {
            return Task.CompletedTask;
        }
    }
}
