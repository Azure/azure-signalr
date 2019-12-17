// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Tests
{
    public class AbortHub : Hub
    {
        public void Abort()
        {
            Context.Abort();
        }
    }
}
