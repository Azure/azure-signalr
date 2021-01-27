// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.IntegrationTests.Infrastructure.MessageOrderTests
{
    public class NoExecutionContextFlowHub : Hub
    {
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
