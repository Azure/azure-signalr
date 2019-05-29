﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class ServiceConnectionManager : IServiceConnectionManager
    {
        private IReadOnlyDictionary<string, IServiceConnectionContainer> _hubConnections = null;

        private readonly object _lock = new object();

        private readonly IReadOnlyList<string> _hubs;
        private readonly string _appName;

        private IServiceConnectionContainer _appConnection;

        public ServiceConnectionStatus Status => throw new NotSupportedException();

        public Task ConnectionInitializedTask => Task.WhenAll(from connection in GetConnections()
                                                              select connection.ConnectionInitializedTask);

        public ServiceConnectionManager(string appName, IReadOnlyList<string> hubs)
        {
            _hubs = hubs ?? Array.Empty<string>();
            if (_hubs.Contains(appName))
            {
                throw new ArgumentException("App name should not be the same as hub name.");
            }

            _hubs = hubs;
            _appName = appName;
        }

        public void Initialize(IServiceConnectionContainerFactory connectionFactory)
        {
            if (connectionFactory == null)
            {
                throw new ArgumentNullException(nameof(connectionFactory));
            }

            if (_hubConnections != null)
            {
                // TODO: log something to indicate the connection is already initialized.
                return;
            }

            lock (_lock)
            {
                if (_hubConnections != null)
                {
                    return;
                }

                var connections = new Dictionary<string, IServiceConnectionContainer>();

                _appConnection = connectionFactory.Create(_appName);

                foreach (var hub in _hubs)
                {
                    var connection = connectionFactory.Create(hub);
                    connections.Add(hub, connection);
                }

                _hubConnections = connections;
            }
        }

        public Task StartAsync()
        {
            return Task.WhenAll(GetConnections().Select(s => s.StartAsync()));
        }

        public Task StopAsync()
        {
            return Task.WhenAll(GetConnections().Select(s => s.StopAsync()));
        }

        public IServiceConnectionContainer WithHub(string hubName)
        {
            if (_hubConnections == null ||!_hubConnections.TryGetValue(hubName, out var connection))
            {
                throw new KeyNotFoundException($"Service connection with Hub {hubName} does not exist");
            }

            return connection;
        }

        public virtual Task WriteAsync(ServiceMessage serviceMessage)
        {
            if (_appConnection == null)
            {
                throw new InvalidOperationException("App connection is not yet initialized.");
            }

            return _appConnection.WriteAsync(serviceMessage);
        }

        public virtual Task<bool> WriteAckableMessageAsync(ServiceMessage serviceMessage, CancellationToken cancellationToken = default)
        {
            if (_appConnection == null)
            {
                throw new InvalidOperationException("App connection is not yet initialized.");
            }

            return _appConnection.WriteAckableMessageAsync(serviceMessage, cancellationToken);
        }

        private IEnumerable<IServiceConnectionContainer> GetConnections()
        {
            if (_appConnection != null)
            {
                yield return _appConnection;
            }

            if (_hubConnections != null)
            {
                foreach (var conn in _hubConnections)
                {
                    yield return conn.Value;
                }
            }
        }
    }
}
