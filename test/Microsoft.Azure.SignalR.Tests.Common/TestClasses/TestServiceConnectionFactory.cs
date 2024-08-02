// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Microsoft.Azure.SignalR.Tests.Common;

internal sealed class TestServiceConnectionFactory(Func<ServiceEndpoint, IServiceConnection> generator = null) : IServiceConnectionFactory
{
    private readonly Func<ServiceEndpoint, IServiceConnection> _generator = generator;

    public ConcurrentDictionary<HubServiceEndpoint, List<IServiceConnection>> CreatedConnections { get; } = new();

    public IServiceConnection Create(HubServiceEndpoint endpoint, IServiceMessageHandler serviceMessageHandler, AckHandler ackHandler, ServiceConnectionType type)
    {
        var conn = _generator?.Invoke(endpoint) ?? new TestServiceConnection(serviceMessageHandler: serviceMessageHandler);
        var receiver = CreatedConnections.GetOrAdd(endpoint, e => []);
        receiver.Add(conn);
        return conn;
    }
}