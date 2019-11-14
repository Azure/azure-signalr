﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    internal sealed class TestClientConnectionManager : IClientConnectionManager
    {
        private readonly IServiceConnection _serverConnection;

        private readonly bool _contains;

        private readonly ConcurrentDictionary<string, TaskCompletionSource<ConnectionContext>> _waitForConnectionOpen = new ConcurrentDictionary<string, TaskCompletionSource<ConnectionContext>>();

        public ConcurrentDictionary<string, TestTransport> CurrentTransports = new ConcurrentDictionary<string, TestTransport>();

        public TestClientConnectionManager(IServiceConnection serverConnection = null, bool contains = false)
        {
            _serverConnection = serverConnection;
            _contains = contains;
        }

        public Task WhenAllCompleted()
        {
            return Task.CompletedTask;
        }

        public Task<IServiceTransport> CreateConnection(OpenConnectionMessage message, IServiceConnection serviceConnection)
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

        public bool TryAdd(string connectionId, IServiceConnection serviceConnection)
        {
            return true;
        }

        public bool TryGetServiceConnection(string key, out IServiceConnection serviceConnection)
        {
            serviceConnection = _serverConnection;
            return _contains;
        }

        public bool TryRemoveServiceConnection(string connectionId, out IServiceConnection connection)
        {
            connection = null;
            return CurrentTransports.TryRemove(connectionId, out _);
        }

        public Task WaitForClientConnectAsync(string connectionId)
        {
            var tcs = _waitForConnectionOpen.GetOrAdd(connectionId, i => new TaskCompletionSource<ConnectionContext>(TaskCreationOptions.RunContinuationsAsynchronously));

            return tcs.Task;
        }
    }
}