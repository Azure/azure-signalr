// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.IntegrationTests.Infrastructure;

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
        private object _addRemoveLock = new object();
        private ConcurrentBag<MockServiceSideConnection> _serviceSideConnections = new ConcurrentBag<MockServiceSideConnection>();
        private ConcurrentBag<MockServiceConnection> _sdkSideConnections = new ConcurrentBag<MockServiceConnection>();

        public List<MockServiceSideConnection> ServiceSideConnections => _serviceSideConnections.ToList();

        public IInvocationBinder CurrentInvocationBinder { get; set; } = new DefaultMockInvocationBinder();

        public bool RemoveUnregisteredConnections { get; set; } = false;

        public Task AllConnectionsEstablished()
        {
            return Task.WhenAll(_serviceSideConnections.Select(
                c => c.SDKSideServiceConnection.MyMockServiceConnetion.ConnectionInitializedTask));
        }

        public void RegisterSDKConnection(MockServiceConnection sdkSideConnection)
        {
            lock (_addRemoveLock)
            {
                _sdkSideConnections.Add(sdkSideConnection);
            }
        }

        public MockServiceSideConnection RegisterSDKConnectionContext(MockServiceConnectionContext sdkSideConnCtx, HubServiceEndpoint endpoint, string target, IDuplexPipe pipe)
        {
            lock (_addRemoveLock)
            {
                // Loosely coupled and indirect way of instantiating SDK side ServiceConnection and ServiceConnectionContext objects created
                // a unique problem: MockServiceConnection has no way of knowing which instance of MockServiceConnectionContext it is using.
                // Being able to associate these two is useful in several ways:
                // - the mock service (and the tests) would be able to track both ServiceConnectionContext and ServiceConnection states
                // - allows integration tests framework to add additional invariant checks regarding the state of these 2 objects
                // - simplifies cleanup and memory leaks tracking
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
                if (svcConnection == null)
                {
                    System.Threading.Thread.Sleep(1111);
                    svcConnection = _sdkSideConnections.Where(c => c.ConnectionNumber == serviceConnectionIndex).FirstOrDefault();
                }


                Debug.Assert(svcConnection != default, $"Missing MockServiceConnection with id {id}");

                // Found it! MockServiceConnectionContext, please meet the MockServiceConnection instance 
                // which wraps ServiceConnection that is going to use you to send and receive messages 
                sdkSideConnCtx.MyMockServiceConnetion = svcConnection;
                svcConnection.MyConnectionContext = sdkSideConnCtx;

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
        }

        public void UnregisterMockServiceSideConnection(MockServiceSideConnection conn)
        {
            if (!RemoveUnregisteredConnections)
            {
                return;
            }

            // lock to ensure we don't loose any connections while enumerating
            lock (_addRemoveLock)
            {
                var new_serviceSideConnections = new ConcurrentBag<MockServiceSideConnection>();
                while (_serviceSideConnections.TryTake(out var c))
                {
                    if (c != conn)
                    {
                        new_serviceSideConnections.Add(c);
                    }
                    else
                    {
                        //also try removing its sdk side part
                        UnregisterMockServiceConnection(conn.SDKSideServiceConnection);
                    }
                }
                _serviceSideConnections = new_serviceSideConnections;
            }
        }

        public void UnregisterMockServiceConnection(MockServiceConnectionContext conn)
        {
            if (!RemoveUnregisteredConnections)
            {
                return;
            }

            lock (_addRemoveLock)
            {               
                var new_sdkSideConnections = new ConcurrentBag<MockServiceConnection>();
                while (_sdkSideConnections.TryTake(out var c))
                {
                    if (c.MyConnectionContext != conn)
                    {
                        new_sdkSideConnections.Add(c);
                    }
                    else
                    {
                        var svcSideConn = c.MyConnectionContext.MyServiceSideConnection;
                        UnregisterMockServiceSideConnection(svcSideConn);
                    }
                }
                _sdkSideConnections = new_sdkSideConnections;
            }
        }

        internal class DefaultMockInvocationBinder : IInvocationBinder
        {
            public IReadOnlyList<Type> GetParameterTypes(string methodName)
            {
                return new List<Type>(new Type[] { });
            }

            public Type GetReturnType(string invocationId)
            {
                return typeof(object);
            }

            public Type GetStreamItemType(string streamId)
            {
                return typeof(object);
            }
        }
    }
}
