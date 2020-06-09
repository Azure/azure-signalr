﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    internal sealed class TestClientConnectionManager : IClientConnectionManager
    {
        private readonly IServiceConnection _serviceConnection;

        private readonly ConcurrentDictionary<string, TaskCompletionSource<ConnectionContext>> _waitForConnectionOpen = new ConcurrentDictionary<string, TaskCompletionSource<ConnectionContext>>();

        public ConcurrentDictionary<string, TestTransport> CurrentTransports = new ConcurrentDictionary<string, TestTransport>();

        private ConcurrentDictionary<string, ClientConnectionContext> _connections = new ConcurrentDictionary<string, ClientConnectionContext>();

        public IReadOnlyDictionary<string, ClientConnectionContext> ClientConnections => _connections;

        public TestClientConnectionManager(IServiceConnection serviceConnection = null)
        {
            _serviceConnection = serviceConnection;
        }

        public Task WhenAllCompleted()
        {
            return Task.CompletedTask;
        }

        public Task<IServiceTransport> CreateConnection(OpenConnectionMessage message)
        {
            var transport = new TestTransport
            {
                ConnectionId = message.ConnectionId
            };
            CurrentTransports.TryAdd(message.ConnectionId, transport);

            var tcs = _waitForConnectionOpen.GetOrAdd(message.ConnectionId, i => new TaskCompletionSource<ConnectionContext>(TaskCreationOptions.RunContinuationsAsynchronously));

            tcs.TrySetResult(null);

            return Task.FromResult<IServiceTransport>(transport);
        }

        public bool TryAddClientConnection(ClientConnectionContext connection)
        {
            return _connections.TryAdd(connection.ConnectionId, connection);
        }

        public bool TryRemoveClientConnection(string connectionId, out ClientConnectionContext connection)
        {
            connection = null;
            return CurrentTransports.TryRemove(connectionId, out _);
        }

        public bool TryGetClientConnection(string connectionId, out ClientConnectionContext connection)
        {
            if (_serviceConnection != null)
            {
                connection = new ClientConnectionContext(_serviceConnection, connectionId);
                return true;
            }
            return _connections.TryGetValue(connectionId, out connection);
        }

        public Task WaitForClientConnectAsync(string connectionId)
        {
            var tcs = _waitForConnectionOpen.GetOrAdd(connectionId, i => new TaskCompletionSource<ConnectionContext>(TaskCreationOptions.RunContinuationsAsynchronously));

            return tcs.Task;
        }
    }
}