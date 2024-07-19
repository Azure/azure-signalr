// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Microsoft.Azure.SignalR.Tests.Common;

internal sealed class TestServiceConnectionFactory : IServiceConnectionFactory
{
    private readonly Func<ServiceEndpoint, IServiceConnection> _generator;

    public ConcurrentDictionary<HubServiceEndpoint, List<IServiceConnection>> CreatedConnections { get; } = new();

    public TestServiceConnectionFactory(Func<ServiceEndpoint, IServiceConnection> generator = null)
    {
        _generator = generator;
    }

    public IServiceConnection Create(HubServiceEndpoint endpoint, IServiceMessageHandler serviceMessageHandler, AckHandler ackHandler, ServiceConnectionType type)
    {
        var conn = _generator?.Invoke(endpoint) ?? new TestServiceConnection(serviceMessageHandler: serviceMessageHandler);
        var receiver = CreatedConnections.GetOrAdd(endpoint, e => new());
        receiver.Add(conn);
        return conn;
    }
}