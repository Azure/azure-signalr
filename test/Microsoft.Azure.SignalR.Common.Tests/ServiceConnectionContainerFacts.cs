// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class ServiceConnectionContainerFacts
    {
        private static readonly ServiceProtocol Protocol = new ServiceProtocol();

        [Fact]
        public async Task TestServiceConnectionContainerWithAllConnectedSucceeeds()
        {
            var container = new ServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
            });

            await container.WriteAsync(new HandshakeResponseMessage());

            await container.WriteAsync("1", new HandshakeResponseMessage());
        }

        [Fact]
        public async Task TestServiceConnectionContainerWithAllDisconnectedThrows()
        {
            var container = new ServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
            });

            await Assert.ThrowsAsync<ServiceConnectionNotActiveException>(
                () => container.WriteAsync(new HandshakeResponseMessage())
                );

            await Assert.ThrowsAsync<ServiceConnectionNotActiveException>(
                () => container.WriteAsync("1", new HandshakeResponseMessage())
                );
        }

        [Fact]
        public async Task TestServiceConnectionContainerWithAllThrowsThrows()
        {
            var container = new ServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(throws: true),
                new TestServiceConnection(throws: true),
                new TestServiceConnection(throws: true),
                new TestServiceConnection(throws: true),
                new TestServiceConnection(throws: true),
                new TestServiceConnection(throws: true),
                new TestServiceConnection(throws: true),
            });

            await Assert.ThrowsAsync<ServiceConnectionNotActiveException>(
                () => container.WriteAsync(new HandshakeResponseMessage())
                );

            await Assert.ThrowsAsync<ServiceConnectionNotActiveException>(
                () => container.WriteAsync("1", new HandshakeResponseMessage())
                );
        }

        [Fact]
        public async Task TestServiceConnectionContainerWithSomeThrows1WriteWithPartitionCanPass()
        {
            var container = new ServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(throws: true),
                new TestServiceConnection(throws: true),
                new TestServiceConnection(throws: true),
                new TestServiceConnection(throws: true),
                new TestServiceConnection(throws: true),
                new TestServiceConnection(throws: true),
                new TestServiceConnection(),
            });

            await container.WriteAsync(new HandshakeResponseMessage());
            await container.WriteAsync("1", new HandshakeResponseMessage());
        }

        [Fact]
        public async Task TestServiceConnectionContainerWithSomeThrows2WriteWithPartitionCanPass()
        {
            var container = new ServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(),
                new TestServiceConnection(throws: true),
                new TestServiceConnection(throws: true),
                new TestServiceConnection(throws: true),
                new TestServiceConnection(throws: true),
                new TestServiceConnection(throws: true),
                new TestServiceConnection(throws: true),
            });

            await container.WriteAsync(new HandshakeResponseMessage());
            await container.WriteAsync("1", new HandshakeResponseMessage());
        }

        [Fact]
        public async Task TestServiceConnectionContainerWithSomeThrows3WriteWithPartitionCanPass()
        {
            var container = new ServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(throws: true),
                new TestServiceConnection(throws: true),
                new TestServiceConnection(),
                new TestServiceConnection(throws: true),
                new TestServiceConnection(throws: true),
                new TestServiceConnection(throws: true),
                new TestServiceConnection(throws: true),
            });

            await container.WriteAsync(new HandshakeResponseMessage());
            await container.WriteAsync("1", new HandshakeResponseMessage());
        }

        private sealed class TestServiceConnection : IServiceConnection
        {
            public ServiceConnectionStatus Status { get; }

            private readonly bool _throws;
            public TestServiceConnection(ServiceConnectionStatus status = ServiceConnectionStatus.Connected, bool throws = false)
            {
                Status = status;
                _throws = throws;
            }

            public Task StartAsync()
            {
                return Task.CompletedTask;
            }

            public Task StopAsync()
            {
                return Task.CompletedTask;
            }

            public Task WriteAsync(ServiceMessage serviceMessage)
            {
                if (_throws)
                {
                    throw new ServiceConnectionNotActiveException();
                }

                return Task.CompletedTask;
            }
        }
    }
}
