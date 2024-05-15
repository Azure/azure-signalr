// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.IntegrationTests.Infrastructure;
using Microsoft.Azure.SignalR.IntegrationTests.Infrastructure.MessageOrderTests;
using Microsoft.Azure.SignalR.IntegrationTests.MockService;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using AspNetTestServer = Microsoft.AspNetCore.TestHost.TestServer;

namespace Microsoft.Azure.SignalR.IntegrationTests
{
    public class MessageOrderTests : VerifiableLoggedTest
    {
        private readonly ITestOutputHelper _output;

        public MessageOrderTests(ITestOutputHelper output) : base(output)
        {
            _output = output;
        }

        [Fact]
        // verifies that outgoing messages from a hub call:
        // - use the same primary service connection the hub call was made from
        // - pick and stick to the same secondary connection(s)
        public async Task OutgoingMessagesUseSameServiceConnection()
        {
            var builder = WebHost.CreateDefaultBuilder()
                .ConfigureServices((IServiceCollection services) =>{})
                .ConfigureLogging(logging => logging.AddXunit(_output))
                .UseStartup<IntegrationTestStartup<MockServiceMessageOrderTestParams, UseSameServiceConnectionHub>>();

            using var server = new AspNetTestServer(builder);
            var mockSvc = (server.Host.Services.GetRequiredService<ServiceHubDispatcher<UseSameServiceConnectionHub>>() 
                as MockServiceHubDispatcher<UseSameServiceConnectionHub>).MockService;
            await mockSvc.AllConnectionsEstablished().OrTimeout();
            List<MockServiceSideConnection> allSvcConns = mockSvc.ServiceSideConnections;

            // A few extra checks (just for this initial test to verify more invariants)
            // Each ServiceEndpoint will have ConnectionCount connections
            Assert.Equal(
                MockServiceMessageOrderTestParams.ConnectionCount * MockServiceMessageOrderTestParams.ServiceEndpoints.Length,
                allSvcConns.Count);
            int endpointCount = allSvcConns.Distinct(new MockServiceSideConnectionEndpointComparer()).Count();
            Assert.Equal(MockServiceMessageOrderTestParams.ServiceEndpoints.Length, endpointCount);

            // specify invocation binder before making calls
            mockSvc.CurrentInvocationBinder = new TestHubBroadcastNCallsInvocationBinder();

            // pick a random primary svc connection to make a client connection
            var priList = allSvcConns.Where(i => i.Endpoint.EndpointType == EndpointType.Primary).ToList();
            await using var primarySvc0 = priList[StaticRandom.Next(priList.Count)];
            var client0 = await primarySvc0.ConnectClientAsync().OrTimeout();

            const int MsgNum = 10;
            await client0.SendMessage("BroadcastNumCalls", new object[] { MsgNum }).OrTimeout();
            var counts = await DrainBroadcastMessages(endpointCount, MsgNum, mockSvc).OrTimeout();

            // Did we got the expected number of calls and all of them stick to exactly one primary?
            var primary = counts.Where(c => c.Key.Endpoint.EndpointType == EndpointType.Primary);
            Assert.Single(primary);

            // the primary is the one we used to send client message
            Assert.Equal(primarySvc0, primary.FirstOrDefault().Key);

            // the primary received MsgNum messages
            Assert.Equal(MsgNum, primary.FirstOrDefault().Value);

            // for every secondary that received the messages verify that
            // - their number equals to the number of seconary endpoints
            // - each received N messages
            var secondary = counts.Where(c => c.Key.Endpoint.EndpointType == EndpointType.Secondary);
            var secondaryEndpoints = MockServiceMessageOrderTestParams.ServiceEndpoints.Where(ep => ep.EndpointType == EndpointType.Secondary);
            Assert.Equal(secondaryEndpoints.Count(), secondary.Count());
            foreach (var conn in secondary)
            {
                Assert.Equal(MsgNum, conn.Value);
            }
        }

        // Helper method to pull raw messages received by the mock service and count them per service connection
        private static async Task<ConcurrentDictionary<MockServiceSideConnection, int>> DrainBroadcastMessages(
            int endpointCount, int msgNum, IMockService mockSvc, 
            ConcurrentDictionary<MockServiceSideConnection, int> counts = null)
        {
            counts ??= new ConcurrentDictionary<MockServiceSideConnection, int>();

            // Each endpoint will get the broadcast message so we should receive endpointCount * MsgNum messages
            for (int ep = 0; ep < endpointCount * msgNum; ep++)
            {
                // we go "peek then take" route because we don't know which secondary connection will receive the messages
                var connWithMessage = await Task.WhenAny(mockSvc.ServiceSideConnections.Select(async c =>
                {
                    bool moreData = await c.WaitToDequeueMessageAsync<BroadcastDataMessage>();
                    Assert.True(moreData);
                    return (c, moreData);
                }));

                var conn = connWithMessage.Result.c;
                var newMsg = await conn.DequeueMessageAsync<BroadcastDataMessage>();

                int msgCount = counts.GetOrAdd(conn, 0);
                counts[conn] = ++msgCount;

                // parse each BroadcastDataMessage and verify this is the correct message
                var hubMessage = ParseBroadcastDataMessageJson(newMsg, mockSvc.CurrentInvocationBinder);
                Assert.True(hubMessage is InvocationMessage);
                var invMsg = hubMessage as InvocationMessage;
                Assert.Equal("Callback", invMsg.Target);

                // finally, get ready to verify the order of messages
                int actualCallbackNum = (int)invMsg.Arguments[0];

                // this check works for both primary and secondary connections
                Assert.Equal(msgCount, actualCallbackNum);
            }
            // todo: verify we received no extra BroadcastDataMessage - need TryPeek method (async with timeout?)
            return counts;
        }

        // Todo: helper for scenarios where we recycle connections so mockSvc contains a mixture of old (closed) and new (active) connections
        //static async Task WaitForAllNewConnectionsEstablished(IMockService mockSvc, int expectedActiveConnCount)

        [Fact]
        // verifies that when original outboud service connection associated with the hub call is closed:
        // - a new service connection for each endpoint is selected 
        // - all subsequent messages use newly selected service connections
        // Note 1: this test is for a scenario which already has much weaker message order guarantees
        // because switching from one connection to another may lead to out of order messages.
        // However there are valid scenarios when the outboud messages are only sent after the original connection is closed.
        // In this case all the messages will be sent over a new connection and the order will be preserved.
        // Note 2: there are multiple ways to close service connection. In this test we ask the mock service to close the pipe.
        // There are several other cases when SDK initiates disconnect which we didn't test here.
        // In either case we need to wait for the connection to be "fully closed" before sending messages.
        public async Task OutgoingMessagesSwitchOverToNewServiceConnection()
        {
            // step 0: create initial connections
            var builder = WebHost.CreateDefaultBuilder()
                .ConfigureServices((IServiceCollection services) => { })
                .ConfigureLogging(logging => logging.AddXunit(_output))
                .UseStartup<IntegrationTestStartup<MockServiceMessageOrderTestParams, SwitchOverToNewServiceConnectionHub>>();

            using var server = new AspNetTestServer(builder);
            var mockSvc = (server.Host.Services.GetRequiredService<ServiceHubDispatcher<SwitchOverToNewServiceConnectionHub>>() 
                as MockServiceHubDispatcher<SwitchOverToNewServiceConnectionHub>).MockService;
            mockSvc.CurrentInvocationBinder = new TestHubBroadcastNCallsInvocationBinder();
            await mockSvc.AllConnectionsEstablished().OrTimeout();
            var allSvcConns = mockSvc.ServiceSideConnections;
            var primarySvc0 = allSvcConns.Where(i => i.Endpoint.EndpointType == EndpointType.Primary).FirstOrDefault();
            var client0 = await primarySvc0.ConnectClientAsync().OrTimeout();

            // step 1: broadcast a message to figure out secondary connection selections
            // we already know the primary connection (primarySvc0) but not the secondary one(s)
            // which will only be selected at the first outgoing message to the service
            await client0.SendMessage("BroadcastNumCalls", new object[] { 1 }).OrTimeout();
            var epCount = MockServiceMessageOrderTestParams.ServiceEndpoints.Length;
            var connSelections = new ConcurrentBag<MockServiceSideConnection>();

            for (int ep = 0; ep < epCount; ep++)
            {
                var connReceivedMessage = await Task.WhenAny(allSvcConns.Select(async c =>
                {
                    await c.WaitToDequeueMessageAsync<BroadcastDataMessage>();
                    return c;
                }));
                connSelections.Add(connReceivedMessage.Result);
                await connReceivedMessage.Result.DequeueMessageAsync<BroadcastDataMessage>().OrTimeout();
            }

            // sanity checks
            Assert.Equal(primarySvc0, connSelections.Where(c => c.Endpoint.EndpointType == EndpointType.Primary).FirstOrDefault());
            var secondaryEpCount = MockServiceMessageOrderTestParams.ServiceEndpoints.Where(ep => ep.EndpointType == EndpointType.Secondary).Count();
            var secondaryReceivedCount = connSelections.Where(c => c.Endpoint.EndpointType == EndpointType.Secondary).Count();
            Assert.Equal(secondaryEpCount, secondaryReceivedCount);

            // step 2: call hub and drop all the connections associated with the current client
            const int MsgNum = 10;
            await client0.SendMessage("BroadcastNumCallsAfterDisconnected", new object[] { MsgNum }).OrTimeout();
            foreach (var secConnUsed in connSelections.Where(c => c.Endpoint.EndpointType == EndpointType.Secondary))
            {
                await secConnUsed.StopAsync().OrTimeout();
            }
            await primarySvc0.StopAsync().OrTimeout();

            // step 3: drain and count messages sent as the result of the call to BroadcastNumCallsAfterDisconnected
            await mockSvc.AllConnectionsEstablished().OrTimeout(); 
            var counts = await DrainBroadcastMessages(epCount, MsgNum, mockSvc).OrTimeout();

            // step 4: verify the connections that received messages
            var primary = counts.Where(c => c.Key.Endpoint.EndpointType == EndpointType.Primary);
            Assert.Single(primary);
            // the primary is NOT the one we used to send client message
            Assert.NotEqual(primary.FirstOrDefault().Key, primarySvc0);
            // and it received MsgNum messages
            Assert.Equal(MsgNum, primary.FirstOrDefault().Value);

            // for every secondary verify that
            // - their number equals to the number of seconary endpoints
            // - each received MsgNum messages
            // - each of the secondary ones is not the same as the original selection
            var secondary = counts.Where(c => c.Key.Endpoint.EndpointType == EndpointType.Secondary);
            var secondaryEndpoints = MockServiceMessageOrderTestParams.ServiceEndpoints.Where(ep => ep.EndpointType == EndpointType.Secondary);
            Assert.Equal(secondaryEndpoints.Count(), secondary.Count());
            foreach (var newSecCon in secondary)
            {
                // none of the new secondary connections are the same as the ones initially used
                foreach (var oldSecCon in connSelections.Where(c => c.Endpoint.EndpointType == EndpointType.Secondary))
                {
                    Assert.NotEqual(newSecCon.Key, oldSecCon);
                }
                // each of the new secondary connections received MsgNum messages
                Assert.Equal(MsgNum, newSecCon.Value);
            }
        }

        [Fact]
        // App server may continue sending messages after both the hub call is finished and the client connection is closed.
        // This test verifies the following strong guarantees:
        // - outgoing messages continue to use the same service connection
        // - the order of messages is preserved
        // This test also verifies that all hub calls over the same client connection 
        // stick to the same primary and secondary service connections
        public async Task OutgoingMessagesOnSameServiceConnectionAfterClientConnectionClosed()
        {
            // step 0: initialize
            var builder = WebHost.CreateDefaultBuilder()
                .ConfigureServices((IServiceCollection services) => { })
                .ConfigureLogging(logging => logging.AddXunit(_output))
                .UseStartup<IntegrationTestStartup<MockServiceMessageOrderTestParams, SameSvcConnAfterClientConnectionClosedHub>>();

            using var server = new AspNetTestServer(builder);
            var mockSvc = (server.Host.Services.GetRequiredService<ServiceHubDispatcher<SameSvcConnAfterClientConnectionClosedHub>>() as MockServiceHubDispatcher<SameSvcConnAfterClientConnectionClosedHub>).MockService;
            var epCount = MockServiceMessageOrderTestParams.ServiceEndpoints.Length;
            mockSvc.CurrentInvocationBinder = new TestHubBroadcastNCallsInvocationBinder();
            await mockSvc.AllConnectionsEstablished().OrTimeout();
            List<MockServiceSideConnection> allSvcConns = mockSvc.ServiceSideConnections;
            await using var primarySvc0 = allSvcConns.Where(i => i.Endpoint.EndpointType == EndpointType.Primary).FirstOrDefault();
            var client0 = await primarySvc0.ConnectClientAsync().OrTimeout();

            // step 1: make sure we know initial connection selections before disconnecting the client
            // make 2 calls to also verify that subsequent calls stick to previous selections
            await client0.SendMessage("BroadcastNumCalls", new object[] { /*numCalls*/ 1, /*countOffset*/ 0 });
            await client0.SendMessage("BroadcastNumCalls", new object[] { /*numCalls*/ 1, /*countOffset*/ 1 });
            var counts = await DrainBroadcastMessages(epCount, 2, mockSvc).OrTimeout();

            // step 2: call hub and drop the client connection
            const int MsgNum = 10;
            const int countOffset = 2; // account for 2 extra messages sent before we disconnected the client
            await client0.SendMessage("BroadcastNumCallsAfterDisconnected", new object[] { MsgNum, countOffset });
            await client0.CloseConnection();

            // step 3: receive and count messages sent as the result of the call to BroadcastNumCallsAfterDisconnected
            await DrainBroadcastMessages(epCount, MsgNum, mockSvc, counts).OrTimeout();
            
            // step 4: verify the connections that received messages
            var primary = counts.Where(c => c.Key.Endpoint.EndpointType == EndpointType.Primary);
            Assert.Single(primary);
            // the primary is the one we used to send client message
            Assert.Equal(primarySvc0, primary.FirstOrDefault().Key);
            // and it received N + 2 messages
            Assert.Equal(MsgNum + countOffset, primary.FirstOrDefault().Value);

            // for every secondary verify that
            // - their number equals to the number of seconary endpoints
            // - each received N + 2 messages
            // - each of the secondary ones is the same as the original selection
            var secondary = counts.Where(c => c.Key.Endpoint.EndpointType == EndpointType.Secondary);
            var secondaryEndpoints = MockServiceMessageOrderTestParams.ServiceEndpoints.Where(ep => ep.EndpointType == EndpointType.Secondary);
            Assert.Equal(secondaryEndpoints.Count(), secondary.Count());
            foreach (var secCon in secondary)
            {
                // each of the new secondary connections received MsgNum + 2 (including initial 2 calls) messages
                Assert.Equal(MsgNum + countOffset, secCon.Value);
            }
        }

        [Fact]
        // Hub method is not flowing execution context but using ClientConnectionScope instead
        public async Task OutgoingMessagesWithoutExecutionContextFlow()
        {
            var builder = WebHost.CreateDefaultBuilder()
                 .ConfigureServices((IServiceCollection services) => { })
                 .ConfigureLogging(logging => logging.AddXunit(_output))
                 .UseStartup<IntegrationTestStartup<MockServiceMessageOrderTestParams, NoExecutionContextFlowHub>>();

            using var server = new AspNetTestServer(builder);
            var mockSvc = (server.Host.Services.GetRequiredService<ServiceHubDispatcher<NoExecutionContextFlowHub>>() as MockServiceHubDispatcher<NoExecutionContextFlowHub>).MockService;
            await mockSvc.AllConnectionsEstablished().OrTimeout();
            var allSvcConns = mockSvc.ServiceSideConnections;
            mockSvc.CurrentInvocationBinder = new TestHubBroadcastNCallsInvocationBinder();
            var priList = allSvcConns.Where(i => i.Endpoint.EndpointType == EndpointType.Primary).ToList();
            await using var primarySvc0 = priList[StaticRandom.Next(priList.Count)];
            var client0 = await primarySvc0.ConnectClientAsync().OrTimeout();

            const int MsgNum = 10;
            await client0.SendMessage("BroadcastNumCallsNotFlowing", new object[] { MsgNum }).OrTimeout();
            int epCount = allSvcConns.Distinct(new MockServiceSideConnectionEndpointComparer()).Count();

            var counts = await DrainBroadcastMessages(epCount, MsgNum, mockSvc).OrTimeout();

            Assert.Equal(MockServiceMessageOrderTestParams.ServiceEndpoints.Count(), counts.Count());
            foreach (var conn in counts)
            {
                Assert.Equal(MsgNum, conn.Value);
            }
        }

        [Fact]
        // OnConnectedAsync and regular hub method calls should have the same connection selection for the same client connection
        // This test verifies this by sending messages from both and verifying the message order.
        public async Task OutgoingMessagesMultipleContexts()
        {
            var builder = WebHost.CreateDefaultBuilder()
                 .ConfigureServices((IServiceCollection services) => { })
                 .ConfigureLogging(logging => logging.AddXunit(_output))
                 .UseStartup<IntegrationTestStartup<MockServiceMessageOrderTestParams, MultipleContextsHub>>();

            using var server = new AspNetTestServer(builder);
            var mockSvc = (server.Host.Services.GetRequiredService<ServiceHubDispatcher<MultipleContextsHub>>() as MockServiceHubDispatcher<MultipleContextsHub>).MockService;
            await mockSvc.AllConnectionsEstablished().OrTimeout();
            List<MockServiceSideConnection> allSvcConns = mockSvc.ServiceSideConnections;
            mockSvc.CurrentInvocationBinder = new TestHubBroadcastNCallsInvocationBinder();
            var priList = allSvcConns.Where(i => i.Endpoint.EndpointType == EndpointType.Primary).ToList();
            await using var primarySvc0 = priList[StaticRandom.Next(priList.Count)];
            var client0 = await primarySvc0.ConnectClientAsync().OrTimeout();

            int epCount = allSvcConns.Distinct(new MockServiceSideConnectionEndpointComparer()).Count();
            const int MsgNum = 10;
            await client0.SendMessage("BroadcastNumCallsMultipleContexts", new object[] { MsgNum }).OrTimeout();
            var counts = await DrainBroadcastMessages(epCount, MsgNum, mockSvc).OrTimeout();

            Assert.Equal(counts.Count(), MockServiceMessageOrderTestParams.ServiceEndpoints.Count());
            foreach (var conn in counts)
            {
                Assert.Equal(MsgNum, conn.Value);
            }
        }

        // Config hot reload allows adding & removing endpoints and corresponding service connections
        // This test verifies that when new endpoits are added, they will be selected for new connections.
        // When old endpoints are removed, the corresponding previously used connections are not leaked.
        //
        // The test makes a service connection C over endpoint E, then makes a hub call which runs a new task.
        // This new task sends messages to the service and its execution context carries connection selection info.
        // When the endpoint E is removed as the result of config hot reload, the corresponding connection C is closed.
        // However the task spawned in the hub call still carries the previous connection selection information.
        //
        // To verify that there are no leaks after the hot reload we wrap the references to the old connection C and endpoint E
        // in weak reference handles and induce a full GC. Then we check if the the weak references targets are nulled out.
        [Fact]
        public async Task PreviouslyUsedServiceConnectionsNotLeakedAfterHotReload2()
        {
            var builder = WebHost.CreateDefaultBuilder()
                 .ConfigureServices((IServiceCollection services) => { })
                 .ConfigureLogging(logging => logging.AddXunit(_output))
                 .UseStartup<HotReloadIntegrationTestStartup<HotReloadMessageOrderTestParams, HotReloadTestHub>>();

            using var server = new AspNetTestServer(builder);
            var wrList = await BroadcastAndHotReloadAllEndpoints(server);

            // here we assume that 2 GCs and 1 finalizer are enough
            await Task.Delay(3300);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(3300);
            GC.Collect();

            foreach (var wr in wrList)
            {
                object o = wr.Target;
                Assert.Null(o);
            }
        }

        public async Task<List<WeakReference>> BroadcastAndHotReloadAllEndpoints(AspNetTestServer server)
        { 
            // Part1: broadcast messages over initial set of endpoints
            var mockSvc = (server.Host.Services.GetRequiredService<ServiceHubDispatcher<HotReloadTestHub>>() as MockServiceHubDispatcher<HotReloadTestHub>).MockService;
            await mockSvc.AllConnectionsEstablished();
            List<MockServiceSideConnection> allSvcConns0 = mockSvc.ServiceSideConnections;
            mockSvc.CurrentInvocationBinder = new TestHubBroadcastNCallsInvocationBinder();

            var priList = allSvcConns0.Where(i => i.Endpoint.EndpointType == EndpointType.Primary).ToList();
            var primarySvc0 = priList[0];
            var client0 = await primarySvc0.ConnectClientAsync();

            const int MsgNum = 10;
            await client0.SendMessage("BroadcastNumCalls", new object[] { MsgNum }).OrTimeout();

            // Todo: properly drain messages from this hub call before hot reload
            // (otherwise they appear on the new endpoints)
            await Task.Delay(3333); // a small delay will normally be enough

            // check and save the refs to the old connections before hot reload
            var wrList = new List<WeakReference>();

            foreach (var svcConn in allSvcConns0)
            {
                Assert.NotNull(svcConn.SDKSideServiceConnection);
                wrList.Add(new WeakReference(svcConn.SDKSideServiceConnection));

                Assert.NotNull(svcConn.SDKSideServiceConnection.MyMockServiceConnetion);
                wrList.Add(new WeakReference(svcConn.SDKSideServiceConnection.MyMockServiceConnetion));

                Assert.NotNull(svcConn.SDKSideServiceConnection.MyMockServiceConnetion.InnerServiceConnection);
                wrList.Add(new WeakReference(svcConn.SDKSideServiceConnection.MyMockServiceConnetion.InnerServiceConnection));

                Assert.NotNull(svcConn.Endpoint);
                wrList.Add(new WeakReference(svcConn.Endpoint));
            }

            // Part2: hot reload and until the old connections are all gone
            mockSvc.RemoveUnregisteredConnections = true;
            HotReloadIntegrationTestStartup<HotReloadMessageOrderTestParams, HotReloadTestHub>.ReloadConfig(index: 1);

            List<MockServiceSideConnection> allSvcConnsNew = null;
            bool allNew = false;
            do
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
                await mockSvc.AllConnectionsEstablished();
                allSvcConnsNew = mockSvc.ServiceSideConnections;
                var newEndpoints = HotReloadMessageOrderTestParams.AllEndpoints[1];
                if (allSvcConnsNew.Count != newEndpoints.Length)
                {
                    continue;
                }

                foreach (var ep in HotReloadMessageOrderTestParams.AllEndpoints[1])
                {
                    allNew = true;
                    bool foundEp = false;
                    foreach (var conn in allSvcConnsNew)
                    {
                        if (conn.Endpoint.ConnectionString == ep.Value)
                        {
                            foundEp = true;
                            break;
                        }
                    }
                    if (!foundEp)
                    {
                        allNew = false;
                        break;
                    }
                }
            } while (!allNew);

            // Part3: send message over the new set of endpoints and verify that only the new endpoints are used
            var primarySvc1 = allSvcConnsNew.Where(i => i.Endpoint.EndpointType == EndpointType.Primary).FirstOrDefault();
            int endpointCount = allSvcConnsNew.Distinct(new MockServiceSideConnectionEndpointComparer()).Count();
            var client1 = await primarySvc1.ConnectClientAsync().OrTimeout();
            await client1.SendMessage("BroadcastNumCalls", new object[] { MsgNum }).OrTimeout();

            var counts = await DrainBroadcastMessages(endpointCount, MsgNum, mockSvc);

            Assert.Equal(HotReloadMessageOrderTestParams.AllEndpoints[1].Count(), counts.Count());
            foreach (var conn in counts)
            {
                Assert.Equal(MsgNum, conn.Value);
            }
            return wrList;        
        }

        private static readonly JsonHubProtocol _signalRPro = new JsonHubProtocol();
        private static HubMessage ParseBroadcastDataMessageJson(BroadcastDataMessage bdm, IInvocationBinder binder)
        {
            foreach (var payload in bdm.Payloads)
            {
                if (payload.Key == "json")
                {
                    var sequence = new ReadOnlySequence<byte>(payload.Value);
                    if (_signalRPro.TryParseMessage(ref sequence, binder, out var signalRRRmessage))
                    {
                        return signalRRRmessage;
                    }
                }
            }
            return null;
        }
    }
}
