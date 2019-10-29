// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.SignalR.Tests
{
    internal sealed class TestServiceConnectionContainer : ServiceConnectionContainerBase
    {
        public TestServiceConnectionContainer(List<IServiceConnection> serviceConnections, HubServiceEndpoint endpoint = null, AckHandler ackHandler = null, IServiceConnectionFactory factory = null)
            : base(factory, 0, endpoint, serviceConnections, ackHandler: ackHandler, logger: NullLogger.Instance)
        {
        }

        public List<IServiceConnection> Connections { get => FixedServiceConnections; }

        public void ShutdownForTest()
        {
            base.StartShutdown();
        }

        public override Task HandlePingAsync(PingMessage pingMessage)
        {
            return Task.CompletedTask;
        }

        protected override Task OnConnectionComplete(IServiceConnection connection)
        {
            return Task.CompletedTask;
        }

        public Task OnConnectionCompleteForTestShutdown(IServiceConnection connection)
        {
            return base.OnConnectionComplete(connection);
        }
    }

    class SimpleTestServiceConnectionFactory : IServiceConnectionFactory
    {
        public IServiceConnection Create(HubServiceEndpoint endpoint, IServiceMessageHandler serviceMessageHandler, ServerConnectionType type)
        {
            return new SimpleTestServiceConnection();
        }
    }

    class SimpleTestServiceConnection : IServiceConnection
    {
        public ServiceConnectionStatus Status { get; set; }

        public Task ConnectionInitializedTask => Task.CompletedTask;

        public event Action<StatusChange> ConnectionStatusChanged;

        public SimpleTestServiceConnection(ServiceConnectionStatus status = ServiceConnectionStatus.Disconnected)
        {
            Status = status;
        }

        public Task CloseAsync(TimeSpan timeout)
        {
            throw new NotImplementedException();
        }

        public Task StartAsync(string target = null)
        {
            throw new NotImplementedException();
        }

        public Task StopAsync()
        {
            throw new NotImplementedException();
        }

        public Task WriteAsync(ServiceMessage serviceMessage)
        {
            throw new NotImplementedException();
        }
    }
}
