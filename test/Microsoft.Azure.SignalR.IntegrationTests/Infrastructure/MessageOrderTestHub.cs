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

        private static TaskCompletionSource<bool> s_tcs;
        public bool BroadcastNumCallsAfterDisconnected(int numCalls)
        {
            s_tcs = new TaskCompletionSource<bool>();
            var all = Clients.All;
            Task.Run(async () =>
            {
                // only start sending after the client connection is dropped
                await s_tcs.Task;

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

        public override Task OnDisconnectedAsync(Exception exception)
        {
            s_tcs?.SetResult(true);
            return Task.CompletedTask;
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
    }
}