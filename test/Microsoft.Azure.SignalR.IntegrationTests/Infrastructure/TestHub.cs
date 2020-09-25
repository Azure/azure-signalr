// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.IntegrationTests.Infrastructure
{
    public class TestHub : Hub
    {
        public async Task<bool> BroadcastNumCalls(int numCalls)
        {
            for (int i = 0; i < numCalls; )
            {
                await Clients.All.SendAsync("Callback", ++i);
            }
            return true;
        }
    }
}