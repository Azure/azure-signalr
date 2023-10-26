// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management;

internal class ExponentialBackOffPolicy : IBackOffPolicy
{
    private readonly int _maxRetries;
    private readonly TimeSpan _minDelay;
    private readonly TimeSpan _maxDelay;

    public ExponentialBackOffPolicy(IOptions<ServiceManagerOptions> options)
    {
        var retryOptions = options.Value.RetryOptions ?? throw new ArgumentException();
        if (retryOptions.Mode != ServiceManagerRetryMode.Exponential)
        {
            throw new ArgumentException();
        }
        _maxRetries = retryOptions.MaxRetries;
        _minDelay = retryOptions.Delay;
        _maxDelay = retryOptions.MaxDelay;
    }
    public IEnumerable<TimeSpan> GetDelays()
    {
        var lastDelay = TimeSpan.MinValue;
        for (var i = 0; i < _maxRetries; i++)
        {
            if (lastDelay >= _maxDelay)
            {
                yield return _maxDelay;
            }
            else
            {
                var delay = TimeSpan.FromMilliseconds((1 << i) * (int)_minDelay.TotalMilliseconds);
                lastDelay = delay < _maxDelay ? delay : _maxDelay;
                yield return lastDelay;
            }
        }
    }
}
