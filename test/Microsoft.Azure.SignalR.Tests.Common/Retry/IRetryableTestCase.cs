// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit.Sdk;

namespace Microsoft.Azure.SignalR.Tests;

public interface IRetryableTestCase : IXunitTestCase
{
    int MaxRetries { get; }

    int DelayBetweenRetriesMs { get; }

    string[] SkipOnExceptionFullNames { get; }
}
