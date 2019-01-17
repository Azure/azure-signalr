// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal class StrongServiceConnectionContainer : ServiceConnectionContainerBase
    {
        private readonly List<IServiceConnection> _onDemandServiceConnections;

        // The lock is only used to lock the on-demand part
        private readonly object _lock = new object();

        public StrongServiceConnectionContainer(IServiceConnectionFactory serviceConnectionFactory,
            IConnectionFactory connectionFactory,
            int fixedConnectionCount) : base(serviceConnectionFactory, connectionFactory, fixedConnectionCount)
        {
            _onDemandServiceConnections = new List<IServiceConnection>();
        }

        // For test purpose only
        internal StrongServiceConnectionContainer(IServiceConnectionFactory serviceConnectionFactory,
            IConnectionFactory connectionFactory, List<IServiceConnection> initialConnections) : base(
            serviceConnectionFactory, connectionFactory, initialConnections)
        {
            _onDemandServiceConnections = new List<IServiceConnection>();
        }

        protected override IServiceConnection CreateServiceConnectionCore()
        {
            return CreateServiceConnectionCore(ServerConnectionType.Default);
        }

        public override IServiceConnection CreateServiceConnection()
        {
            IServiceConnection newConnection;

            lock (_lock)
            {
                newConnection = CreateServiceConnectionCore(ServerConnectionType.OnDemand);
                _onDemandServiceConnections.Add(newConnection);
            }

            return newConnection;
        }

        public override void DisposeServiceConnection(IServiceConnection connection)
        {
            throw new NotImplementedException();
        }
    }
}
