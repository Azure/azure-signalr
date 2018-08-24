﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class ServiceConnectionContainer : IServiceConnectionContainer
    {
        private readonly List<IServiceConnection> _serviceConnections;

        public ServiceConnectionContainer(Func<IServiceConnection> generator, int count)
        {
            if (generator == null)
            {
                throw new ArgumentNullException(nameof(generator));
            }

            if (count <= 0)
            {
                throw new ArgumentException($"{nameof(count)} must be greater than 0.");
            }

            _serviceConnections = new List<IServiceConnection>(count);
            for (int i = 0; i < count; i++)
            {
                _serviceConnections.Add(generator());
            }
        }

        public Task StartAsync()
        {
            return Task.WhenAll(_serviceConnections.Select(c => c.StartAsync()));
        }

        public Task StopAsync()
        {
            return Task.WhenAll(_serviceConnections.Select(c => c.StopAsync()));
        }

        public Task WriteAsync(ServiceMessage serviceMessage)
        {
            var index = StaticRandom.Next(_serviceConnections.Count);
            return _serviceConnections[index].WriteAsync(serviceMessage);
        }

        public async Task WriteAsync(string partitionKey, ServiceMessage serviceMessage)
        {
            if (_serviceConnections.Count == 0) return;

            // If we hit this check, it is a code bug.
            if (string.IsNullOrEmpty(partitionKey))
            {
                throw new ArgumentNullException(nameof(partitionKey));
            }

            var index = Math.Abs(partitionKey.GetHashCode() % _serviceConnections.Count);
            await _serviceConnections[index].WriteAsync(serviceMessage);
        }
    }
}
