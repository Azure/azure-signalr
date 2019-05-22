﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Tests
{
    public class TestHub : Hub
    {
        public void Echo(string message)
        {
            Clients.Client(Context.ConnectionId).SendAsync(nameof(Echo), message);
        }
    }
}