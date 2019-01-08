// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests
{
    public class ServiceConnectionBaseTests
    {
        [Theory]
        [InlineData(0, 1, 1, 2)]
        [InlineData(1, 2, 2, 3)]
        [InlineData(2, 3, 4, 5)]
        [InlineData(3, 4, 8, 9)]
        [InlineData(4, 5, 16, 17)]
        [InlineData(5, 6, 32, 33)]
        [InlineData(6, 6, 60, 61)]
        [InlineData(600, 600, 60, 61)]
        public void TestGetRetryDelay(int count, int exitCount, int minSeconds, int maxSeconds)
        {
            var c = count;
            var span = ServiceConnectionBase.GetRetryDelay(ref c);
            Assert.Equal(exitCount, c);
            Assert.True(TimeSpan.FromSeconds(minSeconds) <= span);
            Assert.True(TimeSpan.FromSeconds(maxSeconds) >= span);
        }
    }
}
