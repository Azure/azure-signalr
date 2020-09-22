// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests.MultiEndpoints
{
    public static class TaskExtension
    {
        public static async Task AssertThrowAggregationException(this Task t, int innerExceptionCounts)
        {
            try
            {
                await t;
            }
            catch
            {
                var aggregationExp = t.Exception;
                Assert.IsType<AggregateException>(aggregationExp);
                Assert.Equal(innerExceptionCounts, aggregationExp.Flatten().InnerExceptions.Count);
                return;
            }
            Assert.True(false, "Exception is not thrown as excepted.");
        }
    }
}