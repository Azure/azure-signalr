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
        public MockServiceSideConnection RegisterSDKConnectionContext(MockServiceConnectionContext sdkSIdeConnCtx, HubServiceEndpoint endpoint, string target, IDuplexPipe pipe);
        public void RegisterSDKConnection(MockServiceConnection sdkSideConnection);
        public bool RemoveUnregisteredConnections { get; set; }
        public void UnregisterMockServiceSideConnection(MockServiceSideConnection conn);
        public void UnregisterMockServiceConnection(MockServiceConnectionContext conn);
        List<MockServiceSideConnection> ServiceSideConnections { get; }
        IInvocationBinder CurrentInvocationBinder { get; set; }
        Task AllConnectionsEstablished();
    }
}