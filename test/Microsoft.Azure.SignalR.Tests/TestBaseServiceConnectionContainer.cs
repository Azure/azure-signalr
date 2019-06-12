// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.Tests
{
    internal sealed class TestBaseServiceConnectionContainer : ServiceConnectionContainerBase
    {
        public TestBaseServiceConnectionContainer(List<IServiceConnection> serviceConnections, HubServiceEndpoint endpoint = null)
            : base(null, 0, endpoint, serviceConnections)
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
