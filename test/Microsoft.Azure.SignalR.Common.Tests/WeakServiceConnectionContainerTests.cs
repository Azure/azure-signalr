// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests;

public class WeakServiceConnectionContainerTests
{
    [Theory]
    [InlineData(new bool[] { true }, 1, 0, true)]
    [InlineData(new bool[] { true }, 2, 0, true)]
    [InlineData(new bool[] { true }, 1, 10000, true)]
    [InlineData(new bool[] { true }, 2, 10000, true)]
    [InlineData(new bool[] { true, false }, 2, 0, true)] // count not meet
    [InlineData(new bool[] { false, false }, 2, 10000, true)] // time not meet
    [InlineData(new bool[] { true, false, true }, 2, 0, true)] // whenever true comes
    [InlineData(new bool[] { true, false, false, true }, 2, 0, true)]  // whenever true comes
    [InlineData(new bool[] { true, false, false, true, false }, 2, 0, true)]  // whenever true comes
    [InlineData(new bool[] { false }, 2, 0, true)]
    [InlineData(new bool[] { false, false, true, false, false }, 2, 10000, true)]
    [InlineData(new bool[] { false }, 1, 0, false)]
    [InlineData(new bool[] { false, false }, 2, 0, false)]
    [InlineData(new bool[] { false, true, false, false, false }, 3, 0, false)]
    [InlineData(new bool[] { false, false, true, true, false, false, false, false }, 3, 0, false)]
    public void TestGetServiceStatus(bool[] pingStatus, int checkWindow, int checkMilli, bool expectedStatus)
    {
        var endpoint = new TestHubServiceEndpoint();
        var container = new WeakServiceConnectionContainer(null, 0, endpoint, NullLogger.Instance);
        var checkTimeSpan = TimeSpan.FromMilliseconds(checkMilli);
        var status = true;
        foreach (var ping in pingStatus)
        {
            status = container.GetServiceStatus(ping, checkWindow, checkTimeSpan);
        }

        Assert.Equal(expectedStatus, status);
    }
}
