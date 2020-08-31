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
using System.Diagnostics;

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
    internal class ConnectionTrackingMockService : IMockService
    {
        ConcurrentBag<MockServiceSideConnection> _serviceSideConnections = new ConcurrentBag<MockServiceSideConnection>();
        ConcurrentBag<MockServiceConnection> _sdkSideConnections = new ConcurrentBag<MockServiceConnection>();

        public List<MockServiceSideConnection> ServiceSideConnections => _serviceSideConnections.ToList();

        public IInvocationBinder CurrentInvocationBinder { get; set; } = new DefaultMockInvocationBinder();

        public Task AllConnectionsEstablished()
        {
            return Task.WhenAll(_serviceSideConnections.Select(
                c => c.SDKSideServiceConnection.MyMockServiceConnetion.ConnectionInitializedTask));
        }

        public void RegisterSDKConnection(MockServiceConnection sdkSideConnection)
        {
            _sdkSideConnections.Add(sdkSideConnection);
        }

        public MockServiceSideConnection RegisterSDKConnectionContext(MockServiceConnectionContext sdkSideConnCtx, HubServiceEndpoint endpoint, string target, IDuplexPipe pipe)
        {
            // Loosely coupled and indirect way of instantiating SDK side ServiceConnection and ServiceConnectionContext objects created
            // a unique problem: MockServiceConnection has no way of knowing which instance of MockServiceConnectionContext it is using.
            // Being able to associate these two is useful in several ways:
            // - the mock service (and the tests) would be able to track both ServiceConnectionContext and ServiceConnection states
            // - allows integration tests framework to add additional invariant checks regarding the state of these 2 objects
            //
            // The closest thing these 2 share is connectionId. 
            // However ServiceConnection does not expose it via IServiceConnection and therefore its not available to MockServiceConnection
            //
            // Rather than modifying interfaces or resort to using async locals to flow the information 
            // we piggy back on the only parameter that we can control 
            // as it flows from MockServiceConnection instance to MockServiceConnectionContext 
            // see MockServiceConnection.StartAsync
            string startTag = "svc_";
            Debug.Assert(target.IndexOf(startTag) == 0);

            int endTagIndex = target.IndexOf(value: "_", startIndex: startTag.Length);
            Debug.Assert(endTagIndex >= startTag.Length + 1);

            string id = target.Substring(startTag.Length, endTagIndex - startTag.Length);
            int.TryParse(id, out int serviceConnectionIndex);
            Debug.Assert(serviceConnectionIndex > 0);   // indexes start from 1

            var svcConnection = _sdkSideConnections.Where(c => c.ConnectionNumber == serviceConnectionIndex).FirstOrDefault();
            Debug.Assert(svcConnection != default, $"Missing MockServiceConnection with id {id}");

            // Found it! MockServiceConnectionContext, please meet the MockServiceConnection instance 
            // which wraps ServiceConnection that is going to use you to send and receive messages 
            sdkSideConnCtx.MyMockServiceConnetion = svcConnection;

            // now fix the target
            if (target.Length > endTagIndex + 1)
            {
                target = target.Substring(endTagIndex + 1);
            }
            else
            {
                target = null;
            }

            var conn = new MockServiceSideConnection(this, sdkSideConnCtx, endpoint, target, pipe);
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
