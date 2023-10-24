// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management;

internal class FixedBackOffPolicy : IBackOffPolicy
{
    private readonly int _maxRetries;
    private readonly TimeSpan _delay;
    public FixedBackOffPolicy(IOptions<ServiceManagerOptions> options)
    {
        var retryOptions = options.Value.RetryOptions ?? throw new ArgumentException();
        if (retryOptions.Mode != ServiceManagerRetryMode.Fixed)
        {
            throw new ArgumentException();
        }
        _maxRetries = retryOptions.MaxRetries;
        _delay = retryOptions.Delay;
    }

    public IEnumerable<TimeSpan> GetDelays()
    {
        for (var i = 0; i < _maxRetries; i++)
        {
            yield return _delay;
        }
    }
}
