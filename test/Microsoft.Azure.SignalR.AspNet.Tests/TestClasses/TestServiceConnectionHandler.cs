// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.AspNet.Tests;

internal sealed class TestServiceConnectionHandler : ServiceConnectionManager
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<Type, TaskCompletionSource<ServiceMessage>> _waitForTransportOutputMessage = new();

    public TestServiceConnectionHandler(ILoggerFactory loggerFactory) : this(loggerFactory, null, null)
    {
    }

    public TestServiceConnectionHandler(ILoggerFactory loggerFactory, string appName, IReadOnlyList<string> hubs) : base(appName, hubs)
    {
        _logger = loggerFactory.CreateLogger<TestServiceConnectionHandler>();
    }

    public override Task WriteAsync(ServiceMessage serviceMessage)
    {
        if (_waitForTransportOutputMessage.TryGetValue(serviceMessage.GetType(), out var tcs))
        {
            _logger.LogInformation($"Set TCS for {serviceMessage.GetType().Name}");
            tcs.SetResult(serviceMessage);
        }
        else
        {
            _logger.LogInformation($"Set TCS for {serviceMessage.GetType().Name} failed");
            throw new InvalidOperationException("Not expected to write before tcs is inited");
        }

        return Task.CompletedTask;
    }

    public override Task<bool> WriteAckableMessageAsync(ServiceMessage serviceMessage, CancellationToken cancellationToken = default)
    {
        if (_waitForTransportOutputMessage.TryGetValue(serviceMessage.GetType(), out var tcs))
        {
            tcs.SetResult(serviceMessage);
        }
        else
        {
            throw new InvalidOperationException("Not expected to write before tcs is inited");
        }

        return Task.FromResult(true);
    }

    public Task<ServiceMessage> WaitForTransportOutputMessageAsync(Type messageType)
    {
        var toAdd = new TaskCompletionSource<ServiceMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        // re-init the tcs
        var tcs = _waitForTransportOutputMessage.AddOrUpdate(messageType, t =>
        {
            _logger.LogInformation($"Add TCS for {messageType.Name}");
            return toAdd;
        }, (t, old) =>
        {
            _logger.LogInformation($"Update TCS for {messageType.Name}");
            old.TrySetCanceled();
            return toAdd;
        });
        return tcs.Task;
    }

    public void DisposeServiceConnection(IServiceConnection _)
    {
    }
}
