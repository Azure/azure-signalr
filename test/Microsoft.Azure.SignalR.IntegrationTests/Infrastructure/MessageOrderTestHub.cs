// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
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

        // note: to be used only from BroadcastNumCallsAfterDisconnected
        private static TaskCompletionSource<bool> s_disconnectedTcs;
        
        public override Task OnDisconnectedAsync(Exception exception)
        {
            base.OnDisconnectedAsync(exception);
            s_disconnectedTcs?.SetResult(true);
            return Task.CompletedTask;
        }

        public bool BroadcastNumCallsAfterDisconnected(int numCalls)
        {
            s_disconnectedTcs = new TaskCompletionSource<bool>();
            var all = Clients.All;
            Task.Run(async () =>
            {
                // only start sending after the client connection is dropped
                await s_disconnectedTcs.Task;

                // Note: notifying and updating connection state is not an atomic operation and as the result
                // OnDisconnectedAsync is triggered before both service and client connections state is updated.
                // If we start sending messages immediately we might end up trying to send some using the old connection.
                // The current product implementation will result in a potential loss of messages so
                // the current test will end up hanging waiting for all the messages it expects.
                // To avoid this we introduce a small delay to allow the connection state to propagate.
                // The future product fixes should help with this problem.

                await Task.Delay(2222);
                for (int i = 0; i < numCalls;)
                {
                    await all.SendAsync("Callback", ++i);
                }
            });
            return true;
        }

        public bool BroadcastNumCallsNotFlowing(int numCalls)
        {
            var all = Clients.All;
            using (ExecutionContext.SuppressFlow())
            {
                Task.Run(async () =>
                {
                    using (new ClientConnectionScope())
                    {
                        for (int i = 0; i < numCalls;)
                        {
                            await all.SendAsync("Callback", ++i);
                        }
                    }
                });
            }
            return true;
        }

        // note: to be used only from BroadcastNumCallsMultipleContexts
        private static TaskCompletionSource<IClientProxy> s_connectedTcs = new TaskCompletionSource<IClientProxy> (TaskCreationOptions.RunContinuationsAsynchronously);
        private static TaskCompletionSource<bool> s_connectedDoneTcs = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);

        public override Task OnConnectedAsync()
        {
             base.OnConnectedAsync();
            
            // this captures the execution context before secondary endpoint selection is made
            Task.Run(async ()=> {
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
                throw new ArgumentException("number of calls must be >= 2", nameof (numCalls));
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