// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.Tests
{
    internal sealed class TestServiceConnectionContainer : ServiceConnectionContainerBase
    {
        public TestServiceConnectionContainer(List<IServiceConnection> serviceConnections, ServiceEndpoint endpoint = null)
            : base(null, null, new ConcurrentDictionary<int, IServiceConnection>(serviceConnections.Select((c, i) => new { c, i }).ToDictionary(x => x.i, x => x.c)), endpoint)
        {
        }

        public override Task HandlePingAsync(PingMessage pingMessage)
        {
            throw new NotImplementedException();
        }

        protected override Task OnConnectionComplete(IServiceConnection connection)
        {
            throw new NotImplementedException();
        }

        protected override IServiceConnection CreateServiceConnectionCore()
        {
            throw new NotImplementedException();
        }
    }
}
