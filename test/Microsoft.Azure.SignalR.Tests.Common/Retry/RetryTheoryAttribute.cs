// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Xunit.Sdk;

namespace Microsoft.Azure.SignalR.Tests;

/// <summary>
/// Attribute that is applied to a method to indicate that it is a theory that should be run
/// by the test runner up to <see cref="RetryFactAttribute.MaxRetries"/> times, until it succeeds.
/// </summary>
[XunitTestCaseDiscoverer("Microsoft.Azure.SignalR.Tests.RetryTheoryDiscoverer", "Microsoft.Azure.SignalR.TestCommon")]
[AttributeUsage(AttributeTargets.Method)]
public class RetryTheoryAttribute : RetryFactAttribute
{
    /// <inheritdoc/>
    public RetryTheoryAttribute(params Type[] skipOnExceptions)
        : base(skipOnExceptions) { }

    /// <inheritdoc/>
    public RetryTheoryAttribute(int maxRetries = DEFAULT_MAX_RETRIES,
                                int delayBetweenRetriesMs = DEFAULT_DELAY_BETWEEN_RETRIES_MS,
                                params Type[] skipOnExceptions)
        : base(maxRetries, delayBetweenRetriesMs, skipOnExceptions) { }
}
