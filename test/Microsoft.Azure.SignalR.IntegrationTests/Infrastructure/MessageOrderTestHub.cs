// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.IntegrationTests.Infrastructure
{
    public class MessageOrderTestHub : Hub
    {
        public async Task<bool> BroadcastNumCalls(int numCalls)
        {
            for (int i = 0; i < numCalls; )
            {
                await Clients.All.SendAsync("Callback", ++i);
            }
            return true;
        }

        private static TaskCompletionSource<bool> s_tcs;
        public bool BroadcastNumCallsAfterTheCallFinished(int numCalls)
        {
            s_tcs = new TaskCompletionSource<bool>();
            var all = Clients.All;
            Task.Run(async () =>
            {
                // only start sending after the connection is dropped
                await s_tcs.Task;
                await Task.Delay(2222);
                for (int i = 0; i < numCalls;)
                {
                    await all.SendAsync("Callback", ++i);
                }
            });
            return true;
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            s_tcs?.SetResult(true);
            return Task.CompletedTask;
        }
    }
}