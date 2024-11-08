// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    internal class Utils
    {
        public static async Task PollWait(Func<bool> pollFunc, int count = 5)
        {
            bool result = false;
            int times = 0;
            while (!result)
            {
                times++;
                result = pollFunc();
                if (result)
                {
                    return;
                }
                else if (times > count)
                {
                    break;
                }

                await Task.Delay(1000);
            }

            Assert.Fail("Poll wait failed");
        }
    }
}
