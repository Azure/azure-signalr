// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.IntegrationTests.Infrastructure.MessageOrderTests
{
    public class HotReloadTestHub : Hub
    {
        public bool BroadcastNumCalls(int numCalls)
        {
            var all = Clients.All;
            Task.Run(async () => {
                for (int i = 0; i < numCalls;)
                {
                    await all.SendAsync("Callback", ++i);
                }

                // hang the task so its execution context (and stuff it carries) is "leaked"
                await Task.Delay(TimeSpan.FromHours(1));
            });
            return true;
        }
    }
}
