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
    internal class ConnectionTrackingMockService : IMockService
    {
        ConcurrentBag<MockServiceSideConnection> _serviceSideConnections = new ConcurrentBag<MockServiceSideConnection>();
        ConcurrentBag<MockServiceConnection> _sdkSideConnections = new ConcurrentBag<MockServiceConnection>();

        public List<MockServiceSideConnection> ServiceSideConnections => _serviceSideConnections.ToList();

        public IInvocationBinder CurrentInvocationBinder { get; set; } = new DefaultMockInvocationBinder();

        public async Task AllInitialFixedConnectionsEstablished()
        {
            bool allConnected;
            do
            {
                allConnected = true;
                foreach (var c in _serviceSideConnections)
                {
                    if (c.SDKSideServiceConnection.MyMockServiceConnetion.Status != ServiceConnectionStatus.Connected)
                    {
                        allConnected = false;
                        break;
                    }
                }
                if (!allConnected)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(10));
                }
            } while (!allConnected);
        }

        public void RegisterSDKConnection(MockServiceConnection sdkSideConnection)
        {
            _sdkSideConnections.Add(sdkSideConnection);
        }

        public MockServiceSideConnection RegisterSDKConnectionContext(MockServiceConnectionContext sdkSIdeConnCtx, HubServiceEndpoint endpoint, string target, IDuplexPipe pipe)
        {
            // Loosely coupled and indirect way of instantiating SDK side ServiceConnection and ServiceConnectionContext objects created
            // a unique problem: MockServiceConnection has no way of knowing which instance of MockServiceConnectionContext its creating.
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
            if (target.IndexOf(startTag) == 0)
            {
                int endTagIndex = target.IndexOf(value: "_", startIndex: startTag.Length);
                if (endTagIndex > 0)
                {
                    string id = target.Substring(startTag.Length, endTagIndex - startTag.Length);
                    if (Int32.TryParse(id, out int serviceConnectionIndex))
                    {
                        var svcConnection = _sdkSideConnections.Where(c => c.ConnectionNumber == serviceConnectionIndex).FirstOrDefault();

                        if (svcConnection != default)
                        {
                            // Found it! 
                            // MockServiceConnectionContext, please meet the MockServiceConnection instance that created you
                            sdkSIdeConnCtx.MyMockServiceConnetion = svcConnection;

                            //now fix the target
                            if (target.Length > endTagIndex + 1)
                            {
                                target = target.Substring(endTagIndex + 1);
                            }
                            else
                            {
                                target = null;
                            }
                        }
                        // else throw
                    }
                    // else throw
                }
                // else throw
            }

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
