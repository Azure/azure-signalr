// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.Tests;

#nullable enable

public static class Skip
{
    /// <summary>
    /// Throws an exception that results in a "Skipped" result for the test.
    /// </summary>
    /// <param name="reason">Reason for the test needing to be skipped</param>
    public static void Always(string? reason = null)
    {
        throw new SkipTestException(reason);
    }
}
