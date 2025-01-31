// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;

using Xunit;
using Xunit.Sdk;

namespace Microsoft.Azure.SignalR.Tests;

/// <summary>
/// Attribute that is applied to a method to indicate that it is a fact that should be run
/// by the test runner up to <see cref="MaxRetries"/> times, until it succeeds.
/// </summary>
[XunitTestCaseDiscoverer("Microsoft.Azure.SignalR.Tests.RetryFactDiscoverer", "Microsoft.Azure.SignalR.TestCommon")]
[AttributeUsage(AttributeTargets.Method)]
public class RetryFactAttribute : FactAttribute
{
    public const int DEFAULT_MAX_RETRIES = 2;

    public const int DEFAULT_DELAY_BETWEEN_RETRIES_MS = 0;

    public int MaxRetries { get; } = DEFAULT_MAX_RETRIES;

    public int DelayBetweenRetriesMs { get; } = DEFAULT_DELAY_BETWEEN_RETRIES_MS;

    public Type[] SkipOnExceptions { get; } = [];

    /// <summary>
    /// Ctor (just skip on exceptions)
    /// </summary>
    /// <param name="skipOnExceptions">Mark the test as skipped when this type of exception is encountered</param>
    public RetryFactAttribute(params Type[] skipOnExceptions)
    {
        SkipOnExceptions = skipOnExceptions ?? Type.EmptyTypes;

        if (SkipOnExceptions.Any(t => !t.IsSubclassOf(typeof(Exception))))
        {
            throw new ArgumentException("Specified type must be an exception", nameof(skipOnExceptions));
        }
    }

    /// <summary>
    /// Ctor (full)
    /// </summary>
    /// <param name="maxRetries">The number of times to attempt to run a test for until it succeeds</param>
    /// <param name="delayBetweenRetriesMs">The amount of time (in ms) to wait between each test run attempt</param>
    /// <param name="skipOnExceptions">Mark the test as skipped when this type of exception is encountered</param>
    public RetryFactAttribute(int maxRetries = DEFAULT_MAX_RETRIES,
                              int delayBetweenRetriesMs = DEFAULT_DELAY_BETWEEN_RETRIES_MS,
                              params Type[] skipOnExceptions) : this(skipOnExceptions)
    {
#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRetries, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(delayBetweenRetriesMs, 0);
#else

        if (maxRetries < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRetries));
        }

        if (delayBetweenRetriesMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delayBetweenRetriesMs));
        }
#endif

        MaxRetries = maxRetries;
        DelayBetweenRetriesMs = delayBetweenRetriesMs;
    }
}
