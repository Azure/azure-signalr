// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    internal sealed class TestServiceConnectionManager : ServiceConnectionManager
    {
        private readonly ConcurrentDictionary<Type, TaskCompletionSource<ServiceMessage>> _waitForTransportOutputMessage = new ConcurrentDictionary<Type, TaskCompletionSource<ServiceMessage>>();

        public TestServiceConnectionManager(): this(null, null)
        {
        }

        public TestServiceConnectionManager(string appName, IReadOnlyList<string> hubs) : base(appName, hubs)
        {
        }

        public override Task WriteAsync(ServiceMessage serviceMessage)
        {
            if (_waitForTransportOutputMessage.TryGetValue(serviceMessage.GetType(), out var tcs))
            {
                tcs.SetResult(serviceMessage);
            }
            else
            {
                throw new InvalidOperationException("Not expected to write before tcs is inited");
            }

            return Task.CompletedTask;
        }

        public override Task WriteAsync(string partitionKey, ServiceMessage serviceMessage)
        {
            if (_waitForTransportOutputMessage.TryGetValue(serviceMessage.GetType(), out var tcs))
            {
                tcs.SetResult(serviceMessage);
            }
            else
            {
                throw new InvalidOperationException("Not expected to write before tcs is inited");
            }

            return Task.CompletedTask;
        }

        public Task WaitForTransportOutputMessageAsync(Type messageType)
        {
            if (_waitForTransportOutputMessage.TryGetValue(messageType, out var tcs))
            {
                tcs.TrySetCanceled();
            }

            // re-init the tcs
            tcs = _waitForTransportOutputMessage[messageType] = new TaskCompletionSource<ServiceMessage>();

            return tcs.Task;
        }

        public IServiceConnection CreateServiceConnection()
        {
            throw new NotImplementedException();
        }

        public void DisposeServiceConnection(IServiceConnection connection)
        {
        }
    }

    internal sealed class TestServiceMessageHandler : IServiceMessageHandler
    {
        public TestServiceMessageHandler()
        {
        }

        public Task HandlePingAsync(PingMessage pingMessage)
        {
            throw new NotImplementedException();
        }
    }
}
