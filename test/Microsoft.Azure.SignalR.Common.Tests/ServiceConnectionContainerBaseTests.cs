// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests
{
    public class ServiceConnectionContainerBaseTests
    {
        [Theory]
        [InlineData(0, 1, 2)]
        [InlineData(1, 2, 3)]
        [InlineData(2, 4, 5)]
        [InlineData(3, 8, 9)]
        [InlineData(4, 16, 17)]
        [InlineData(5, 32, 33)]
        [InlineData(6, 60, 61)]
        [InlineData(600, 60, 61)]
        public void TestGetRetryDelay(int count, int minSeconds, int maxSeconds)
        {
            var c = count;
            var span = ServiceConnectionContainerBase.GetRetryDelay(c);
            Assert.True(TimeSpan.FromSeconds(minSeconds) <= span);
            Assert.True(TimeSpan.FromSeconds(maxSeconds) >= span);
        }
    }
}
