// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    public class SignalRServiceContext<THub> where THub : Hub
    {
        public SignalRServiceContext(IHubContext<THub> hubContext)
        {
            HubContext = hubContext;
        }

        public IHubContext<THub> HubContext { get; set; }
    }
}
