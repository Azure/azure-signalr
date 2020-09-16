// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Xunit;
namespace Microsoft.Azure.SignalR.Management.Tests.MultiEndpoints
{
    public class AggreExcpVerificationHelper
    {
        public async Task AssertIsAggreExp(int expCount, Task t)
        {
            try
            {
                await t;
            }
            catch (Exception e)
            {
                Assert.IsNotType<int>(e);
                var aggregationExp = t.Exception;
                Assert.IsType<AggregateException>(aggregationExp);
                Assert.Equal(expCount, aggregationExp.InnerExceptions.Count);
                return;
            }
            Assert.True(false, "Expected exception is not thrown");
        }

        public async Task AssertIsAggreExp<T>(int expCount, Task<T> t)
        {
            try
            {
                await t;
            }
            catch (Exception e)
            {
                Assert.IsNotType<int>(e);
                var aggregationExp = t.Exception;
                Assert.IsType<AggregateException>(aggregationExp);
                Assert.Equal(expCount, aggregationExp.InnerExceptions.Count);
                return;
            }
            Assert.True(false, "Expected exception is not thrown");
        }
    }
}