// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hosting;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.AspNet.SignalR.Transports;
using Microsoft.Azure.SignalR.Protocol;
using Newtonsoft.Json;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class AzureTransport : IServiceTransport
    {
        private readonly TaskCompletionSource<object> _lifetimeTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly HostContext _context;
        private readonly IMemoryPool _pool;
        private readonly JsonSerializer _serializer;
        private readonly IServiceProtocol _serviceProtocol;

        public AzureTransport(HostContext context, IDependencyResolver resolver)
        {
            _context = context;
            context.Environment[AspNetConstants.Context.AzureSignalRTransportKey] = this;
            _pool = resolver.Resolve<IMemoryPool>();
            _serializer = resolver.Resolve<JsonSerializer>();
            _serviceProtocol = resolver.Resolve<IServiceProtocol>();
        }

        public Func<string, Task> Received { get; set; }

        public Func<Task> Connected { get; set; }

        public Func<Task> Reconnected { get; set; }

        public Func<bool, Task> Disconnected { get; set; }

        public string ConnectionId { get; set; }

        public Task<string> GetGroupsToken()
        {
            return Task.FromResult<string>(null);
        }

        public async Task ProcessRequest(ITransportConnection connection)
        {
            var connected = Connected;
            if (connected != null)
            {
                await connected();
            }

            await _lifetimeTcs.Task;

            var disconnected = Disconnected;
            if (disconnected != null)
            {
                await disconnected(true);
            }
        }

        public Task Send(object value)
        {
            if (_context.Environment.TryGetValue(AspNetConstants.Context.AzureServiceConnectionKey, out var connection) && connection is IServiceConnection serviceConnection)
            {
                // Invoke service connection
                return serviceConnection.WriteAsync(ConnectionId, value, _serviceProtocol, _serializer, _pool);
            }

            throw new InvalidOperationException("No service connection found when sending message");
        }

        public void OnReceived(string value)
        {
            _ = ReceivedTask(value);
        }

        public async Task ReceivedTask(string value)
        {
            var received = Received;
            if (received != null)
            {
                // TODO: Add log
                await received(value);
            }
        }

        public void OnDisconnected() => _lifetimeTcs.TrySetResult(null);
    }
}