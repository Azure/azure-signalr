// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;

namespace Microsoft.Azure.SignalR.Tests.Common
{
    internal sealed class TestServiceConnectionFactory : IServiceConnectionFactory
    {
        private readonly Func<ServiceEndpoint, IServiceConnection> _generator;

        public ConcurrentQueue<IServiceConnection> CreatedConnections { get; } = new ConcurrentQueue<IServiceConnection>();
        
        public TestServiceConnectionFactory(Func<ServiceEndpoint, IServiceConnection> generator = null)
        {
            _generator = generator;
        }

        public IServiceConnection Create(HubServiceEndpoint endpoint, IServiceMessageHandler serviceMessageHandler, ServiceConnectionType type)
        {
            var conn = _generator?.Invoke(endpoint) ?? new TestServiceConnection(serviceMessageHandler: serviceMessageHandler);
            CreatedConnections.Enqueue(conn);
            return conn;
        }
    }
}
