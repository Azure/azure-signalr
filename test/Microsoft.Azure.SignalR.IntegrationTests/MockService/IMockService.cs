// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.IntegrationTests.Infrastructure;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.IntegrationTests.MockService
{
    internal interface IMockService
    {
        public MockServiceSideConnection RegisterSDKConnection(MockServiceConnectionContext sdkSIdeConnCtx, HubServiceEndpoint endpoint, string target, IDuplexPipe pipe);
        public Task StopConnectionAsync(MockServiceSideConnection conn);
        List<MockServiceSideConnection> ServiceSideConnections { get; }
        IInvocationBinder CurrentInvocationBinder { get; set; }
    }
}