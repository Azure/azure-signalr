// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    internal class TestServiceConnectionFactory : IServiceConnectionFactory
    {
        public ConcurrentDictionary<HubServiceEndpoint, List<MessageVerifiableConnection>> CreatedConnections { get; } = new();

        public IServiceConnection Create(HubServiceEndpoint endpoint, IServiceMessageHandler serviceMessageHandler, ServiceConnectionType type)
        {
            var connection = new MessageVerifiableConnection(serviceMessageHandler: serviceMessageHandler);
            var list = CreatedConnections.GetOrAdd(endpoint, (endpoint) => new());
            list.Add(connection);
            return connection;
        }
    }
}