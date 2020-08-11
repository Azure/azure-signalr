// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipelines;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.IntegrationTests.Infrastructure;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.IntegrationTests.MockService
{
    /// <summary>
    /// This ConnectionTrackingMockService provides basic facilities for:
    /// - creating and tracking service and client connections
    /// - basic functions for sending and receiving messages
    /// 
    /// It does not provide 
    /// - routing of messages to clients
    /// - group management
    /// </summary>
    class ConnectionTrackingMockService : IMockService
    {
        ConcurrentBag<MockServiceSideConnection> _serviceSideConnections = new ConcurrentBag<MockServiceSideConnection>();

        public List<MockServiceSideConnection> ServiceSideConnections => _serviceSideConnections.ToList();

        public IInvocationBinder CurrentInvocationBinder { get; set; } = new DefaultMockInvocationBinder();

        public MockServiceSideConnection RegisterSDKConnection(MockServiceConnectionContext sdkSIdeConnCtx, HubServiceEndpoint endpoint, string target, IDuplexPipe pipe)
        {
            var conn = new MockServiceSideConnection(this, sdkSIdeConnCtx, endpoint, target, pipe);
            _serviceSideConnections.Add(conn);
            conn.Start();
            return conn;
        }

        public Task StopConnectionAsync(MockServiceSideConnection conn) => conn.StopAsync();
    }

    internal class DefaultMockInvocationBinder : IInvocationBinder
    {
        public IReadOnlyList<Type> GetParameterTypes(string methodName)
        {
            return new List<Type>(new Type[] { });
        }

        public Type GetReturnType(string invocationId)
        {
            return typeof(void);
        }

        public Type GetStreamItemType(string streamId)
        {
            return typeof(void);
        }
    }

    internal class TestHubBroadcastNCallsInvocationBinder : IInvocationBinder
    {
        public IReadOnlyList<Type> GetParameterTypes(string methodName)
        {
            return new List<Type>(new Type[] { typeof(int) });
        }

        public Type GetReturnType(string invocationId)
        {
            return typeof(void);
        }

        public Type GetStreamItemType(string streamId)
        {
            return typeof(void);
        }
    }

}
