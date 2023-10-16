﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management;

#nullable enable

internal class TimeoutHttpMessageHandler : DelegatingHandler
{
    private readonly TimeSpan _timeout;
    public TimeoutHttpMessageHandler(IOptions<ServiceManagerOptions> serviceManagerOptions)
    {
        var options = serviceManagerOptions.Value;
        if (options.RetryOptions == null)
        {
            // Timeout handled by HttpClient for backward compatibility
            _timeout = Timeout.InfiniteTimeSpan;
        }
        else
        {
            _timeout = options.HttpClientTimeout;
        }
    }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_timeout == Timeout.InfiniteTimeSpan)
        {
            return await base.SendAsync(request, cancellationToken);
        }
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_timeout);
        try
        {
            return await base.SendAsync(request, cts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TaskCanceledException($"The request was canceled due to the configured HttpClient.Timeout of {_timeout.TotalSeconds} seconds elapsing.", new TimeoutException());
        }
    }
}
