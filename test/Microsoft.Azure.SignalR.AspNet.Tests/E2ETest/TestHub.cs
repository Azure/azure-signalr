// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNet.SignalR;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    public class TestHub : Hub
    {
        public void Echo(string message)
        {
            Clients.Caller.Echo(message);
        }
    }
}