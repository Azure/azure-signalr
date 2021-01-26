// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.IntegrationTests.Infrastructure.MessageOrderTests
{
    public class MultipleContextsHub : Hub
    {
        // note: to be used only from BroadcastNumCallsMultipleContexts
        private static TaskCompletionSource<IClientProxy> s_connectedTcs = new TaskCompletionSource<IClientProxy>(TaskCreationOptions.RunContinuationsAsynchronously);
        private static TaskCompletionSource<bool> s_connectedDoneTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public override Task OnConnectedAsync()
        {
            base.OnConnectedAsync();

            // this captures the execution context before secondary endpoint selection is made
            Task.Run(async () => {
                var clients = await s_connectedTcs.Task;

                // this is the first message we send out 
                //so this is the moment when secondary endpoint selection is made
                await clients.SendAsync("Callback", 1);
                s_connectedDoneTcs.TrySetResult(true);
            });

            return Task.CompletedTask;
        }

        public async Task<bool> BroadcastNumCallsMultipleContexts(int numCalls)
        {
            if (numCalls < 2)
            {
                throw new ArgumentException("number of calls must be >= 2", nameof(numCalls));
            }
            var all = Clients.All;
            s_connectedTcs.SetResult(all);
            await s_connectedDoneTcs.Task;

            // by this time the secondary endpoint selection is done and persisted
            for (int i = 1; i < numCalls;)
            {
                await Clients.All.SendAsync("Callback", ++i);
            }
            return true;
        }
    }
}
