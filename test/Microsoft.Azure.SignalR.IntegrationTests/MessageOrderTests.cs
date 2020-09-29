// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.IntegrationTests.Infrastructure;
using Microsoft.Azure.SignalR.IntegrationTests.MockService;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Azure.SignalR.Management;

using AspNetTestServer = Microsoft.AspNetCore.TestHost.TestServer;
using System;

namespace Microsoft.Azure.SignalR.IntegrationTests
{
    public class MessageOrderTests : VerifiableLoggedTest
    {
        private ITestOutputHelper _output;

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
                .UseStartup<IntegrationTestStartup<MockServiceMessageOrderTestParams, MessageOrderTestHub>>();

            using(var server = new AspNetTestServer(builder))
            {
                var mockSvc = (server.Host.Services.GetRequiredService<ServiceHubDispatcher<MessageOrderTestHub>>() as MockServiceHubDispatcher<MessageOrderTestHub>).MockService;
                await mockSvc.AllConnectionsEstablished();
                List<MockServiceSideConnection> allSvcConns = mockSvc.ServiceSideConnections;

                // A few extra checks (just for this initial test to verify more invariants)
                // Each ServiceEndpoint will have ConnectionCount connections
                Assert.Equal(allSvcConns.Count, MockServiceMessageOrderTestParams.ConnectionCount * MockServiceMessageOrderTestParams.ServiceEndpoints.Length);
                int endpointCount = allSvcConns.Distinct(new MockServiceSideConnectionEndpointComparer()).Count();
                Assert.Equal(endpointCount, MockServiceMessageOrderTestParams.ServiceEndpoints.Length);

                // specify invocation binder before making calls
                mockSvc.CurrentInvocationBinder = new TestHubBroadcastNCallsInvocationBinder();

                // pick a random primary svc connection to make a client connection
                var priList = allSvcConns.Where(i => i.Endpoint.EndpointType == EndpointType.Primary).ToList();
                var primarySvc0 = priList[StaticRandom.Next(priList.Count)];
                var client0 = await primarySvc0.ConnectClientAsync();

                const int MsgNum = 10;
                await client0.SendMessage("BroadcastNumCalls", new object[] { MsgNum });

                // The mock service does not route messages to client connections as there are no actual clients connected
                // So we just ask for raw messages received by the mock service and count them per service connection
                // Each endpoint will get the broadcast message so we should receive endpointCount * MsgNum messages
                var counts = new ConcurrentDictionary<MockServiceSideConnection, int>();

                for (int ep = 0; ep < endpointCount * MsgNum ; ep++)
                {
                    // we go "peek then take" route because we don't know which secondary connection will receive the messages
                    var connWithMessage = await Task.WhenAny(allSvcConns.Select(async c =>
                    {
                        bool moreData = await c.WaitToDequeueMessageAsync<BroadcastDataMessage>();
                        Assert.True(moreData);
                        return (c, moreData);
                    }));

                    var conn = connWithMessage.Result.c;
                    var newMsg = await conn.DequeueMessageAsync<BroadcastDataMessage>();

                    int msgCount = counts.GetOrAdd (conn, 0);
                    counts[conn] = ++msgCount;

                    // parse each BroadcastDataMessage and verify this is the correct message
                    var hubMessage = ParseBroadcastDataMessageJson(newMsg, mockSvc.CurrentInvocationBinder);
                    Assert.True(hubMessage is InvocationMessage);
                    var invMsg = hubMessage as InvocationMessage;
                    Assert.Equal("Callback", invMsg.Target);

                    // finally, get ready to verify the order of messages
                    int actualCallbackNum = (int)invMsg.Arguments[0];

                    // this check works for both primary and secondary connections
                    Assert.Equal(actualCallbackNum, msgCount);
                }

                // todo: verify we received no extra BroadcastDataMessage - need TryPeek method (async with timeout?)

                // Did we got the expected number of calls and all of them stick to exactly one primary?
                var primary = counts.Where(c => c.Key.Endpoint.EndpointType == EndpointType.Primary);
                Assert.Single(primary);

                // and the primary is the one we used to send client message
                Assert.Equal(primary.FirstOrDefault().Key, primarySvc0);

                // and it received N messages
                Assert.Equal(primary.FirstOrDefault().Value, MsgNum);

                // for every secondary that received the messages verify that
                // - their number equals to the number of seconary endpoints
                // - each received N messages
                var secondary = counts.Where(c => c.Key.Endpoint.EndpointType == EndpointType.Secondary);
                var secondaryEndpoints = MockServiceMessageOrderTestParams.ServiceEndpoints.Where(ep => ep.EndpointType == EndpointType.Secondary);
                Assert.Equal(secondary.Count(), secondaryEndpoints.Count());
                foreach(var conn in secondary)
                {
                    Assert.Equal(conn.Value, MsgNum);
                }
            }
        }


        [Fact]
        // verifies that when original outboud service connection associated with the hub call is closed:
        // - a new service connection for each endpoint is selected 
        // - all subsequent messages use newly selected service connections
        // Note 1: this test is for a scenario which already has much weaker message order guarantees
        // since switching from one connection to another may lead to out of order messages.
        // However there might be scenarios where the outboud messages are only sent after the original connection is over
        // so the important part is that after the switchover it will stick to the new connection selection 
        // Note 2: in this test we ask the mock service to drop service connection. The other case is when 
        // AppServer SDK initiates disconnect. In either case we need to wait for the connection to be 
        // "fully closed" before sending messages.
        public async Task OutgoingMessagesSwitchOverToNewServiceConnection()
        {
            // step 0: create initial connections
            var builder = WebHost.CreateDefaultBuilder()
                .ConfigureServices((IServiceCollection services) => { })
                .ConfigureLogging(logging => logging.AddXunit(_output))
                .UseStartup<IntegrationTestStartup<MockServiceMessageOrderTestParams, MessageOrderTestHub>>();

            using (var server = new AspNetTestServer(builder))
            {
                var mockSvc = (server.Host.Services.GetRequiredService<ServiceHubDispatcher<MessageOrderTestHub>>() as MockServiceHubDispatcher<MessageOrderTestHub>).MockService;
                mockSvc.CurrentInvocationBinder = new TestHubBroadcastNCallsInvocationBinder();

                await mockSvc.AllConnectionsEstablished();
                List<MockServiceSideConnection> allSvcConns = mockSvc.ServiceSideConnections;
                var primarySvc0 = allSvcConns.Where(i => i.Endpoint.EndpointType == EndpointType.Primary).FirstOrDefault();
                var client0 = await primarySvc0.ConnectClientAsync();

                // step 1: figure out secondary connection selections
                // we already know the primary connection (primarySvc0) but not the secondary one(s)
                // which will only be selected at the first outgoing message to the service
                await client0.SendMessage("BroadcastNumCalls", new object[] { 1 });
                var connSelections = new ConcurrentBag<MockServiceSideConnection>();

                for (int ep = 0; ep < MockServiceMessageOrderTestParams.ServiceEndpoints.Length; ep++)
                {
                    var connReceivedMessage = await Task.WhenAny(allSvcConns.Select(async c =>
                    {
                        await c.WaitToDequeueMessageAsync<BroadcastDataMessage>();
                        return c;
                    }));
                    connSelections.Add(connReceivedMessage.Result);
                    await connReceivedMessage.Result.DequeueMessageAsync<BroadcastDataMessage>();
                }

                // sanity checks
                Assert.Equal(primarySvc0, connSelections.Where(c => c.Endpoint.EndpointType == EndpointType.Primary).FirstOrDefault ());
                var secondaryEpCount = MockServiceMessageOrderTestParams.ServiceEndpoints.Where(ep => ep.EndpointType == EndpointType.Secondary).Count();
                var secondaryReceivedCount = connSelections.Where(c => c.Endpoint.EndpointType == EndpointType.Secondary).Count();
                Assert.Equal(secondaryEpCount, secondaryReceivedCount);

                // step 2: call hub and drop all the connections associated with the current client
                const int MsgNum = 10;
                await client0.SendMessage("BroadcastNumCallsAfterDisconnected", new object[] { MsgNum });
                foreach (var secConnUsed in connSelections.Where(c => c.Endpoint.EndpointType == EndpointType.Secondary))
                {
                    await secConnUsed.StopAsync();
                }
                await primarySvc0.StopAsync();

                // step 3: receive and count messages sent as the result of the last call to BroadcastNumCallsAfterDisconnected
                var counts = new ConcurrentDictionary<MockServiceSideConnection, int>();
                for (int ep = 0; ep < MockServiceMessageOrderTestParams.ServiceEndpoints.Length * MsgNum; ep++)
                {
                    var connReceivedMessage = await Task.WhenAny(mockSvc.ServiceSideConnections.Select(async c =>
                    {
                        bool moreData = await c.WaitToDequeueMessageAsync<BroadcastDataMessage>();
                        Assert.True(moreData);
                        return c;
                    }));

                    var conn = connReceivedMessage.Result;
                    var newMsg = await conn.DequeueMessageAsync<BroadcastDataMessage>();

                    int msgCount = counts.GetOrAdd(conn, 0);
                    counts[conn] = ++msgCount;

                    var hubMessage = ParseBroadcastDataMessageJson(newMsg, mockSvc.CurrentInvocationBinder);
                    Assert.True(hubMessage is InvocationMessage);
                    var invMsg = hubMessage as InvocationMessage;
                    Assert.Equal("Callback", invMsg.Target);

                    // verify the order of messages
                    int actualCallbackNum = (int)invMsg.Arguments[0];
                    Assert.Equal(actualCallbackNum, msgCount);
                }

                // step 4: verify the connections that received messages
                var primary = counts.Where(c => c.Key.Endpoint.EndpointType == EndpointType.Primary);
                Assert.Single(primary);
                // the primary is NOT the one we used to send client message
                Assert.NotEqual(primary.FirstOrDefault().Key, primarySvc0);
                // and it received N messages
                Assert.Equal(primary.FirstOrDefault().Value, MsgNum);

                // for every secondary verify that
                // - their number equals to the number of seconary endpoints
                // - each received N messages
                // - each of the secondary ones is not the same as the original selection
                var secondary = counts.Where(c => c.Key.Endpoint.EndpointType == EndpointType.Secondary);
                var secondaryEndpoints = MockServiceMessageOrderTestParams.ServiceEndpoints.Where(ep => ep.EndpointType == EndpointType.Secondary);
                Assert.Equal(secondary.Count(), secondaryEndpoints.Count());
                foreach (var newSecCon in secondary)
                {
                    // none of the new secondary connections are the same as the ones initially used
                    foreach (var oldSecCon in connSelections.Where(c => c.Endpoint.EndpointType == EndpointType.Secondary))
                    {
                        Assert.NotEqual(newSecCon.Key, oldSecCon);
                    }
                    // each of the new secondary connections received MsgNum messages
                    Assert.Equal(newSecCon.Value, MsgNum);
                }
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
                .UseStartup<IntegrationTestStartup<MockServiceMessageOrderTestParams, MessageOrderTestHub>>();

            using (var server = new AspNetTestServer(builder))
            {
                var mockSvc = (server.Host.Services.GetRequiredService<ServiceHubDispatcher<MessageOrderTestHub>>() as MockServiceHubDispatcher<MessageOrderTestHub>).MockService;
                mockSvc.CurrentInvocationBinder = new TestHubBroadcastNCallsInvocationBinder();

                await mockSvc.AllConnectionsEstablished();
                List<MockServiceSideConnection> allSvcConns = mockSvc.ServiceSideConnections;
                var primarySvc0 = allSvcConns.Where(i => i.Endpoint.EndpointType == EndpointType.Primary).FirstOrDefault();
                var client0 = await primarySvc0.ConnectClientAsync();

                // step 1: make sure we know initial connection selections before disconnecting the client
                // make 2 calls to also verify that subsequent calls sticl to previous selections
                await client0.SendMessage("BroadcastNumCalls", new object[] { 1 });
                await client0.SendMessage("BroadcastNumCalls", new object[] { 1 });
                var counts = new ConcurrentDictionary<MockServiceSideConnection, int>();

                for (int ep = 0; ep < 2 * MockServiceMessageOrderTestParams.ServiceEndpoints.Length; ep++)
                {
                    var receivedMessage = await Task.WhenAny(allSvcConns.Select(async c =>
                    {
                        await c.WaitToDequeueMessageAsync<BroadcastDataMessage>();
                        return c;
                    }));
                    var conn = receivedMessage.Result;
                    int msgCount = counts.GetOrAdd(conn, 0);
                    counts[conn] = ++msgCount;

                    // the message was only "peeked", now remove the message to avoid peeking it again
                    await conn.DequeueMessageAsync<BroadcastDataMessage>();
                }

                // step 2: call hub and drop the client connection
                const int MsgNum = 10;
                await client0.SendMessage("BroadcastNumCallsAfterDisconnected", new object[] { MsgNum });
                await client0.CloseConnection();

                // step 3: receive and count messages sent as the result of the call to BroadcastNumCallsAfterDisconnected
                for (int ep = 0; ep < MockServiceMessageOrderTestParams.ServiceEndpoints.Length * MsgNum; ep++)
                {
                    var receivedMessage = await Task.WhenAny(mockSvc.ServiceSideConnections.Select(async c =>
                    {
                        bool moreData = await c.WaitToDequeueMessageAsync<BroadcastDataMessage>();
                        Assert.True(moreData);
                        return c;
                    }));

                    var conn = receivedMessage.Result;
                    var newMsg = await conn.DequeueMessageAsync<BroadcastDataMessage>();

                    int msgCount = counts.GetOrAdd(conn, 0);
                    counts[conn] = ++msgCount;

                    var hubMessage = ParseBroadcastDataMessageJson(newMsg, mockSvc.CurrentInvocationBinder);
                    Assert.True(hubMessage is InvocationMessage);
                    var invMsg = hubMessage as InvocationMessage;
                    Assert.Equal("Callback", invMsg.Target);

                    // verify the order of messages
                    int actualCallbackNum = (int)invMsg.Arguments[0];

                    // account for 2 extra messages sent before we disconnected the client
                    Assert.Equal(actualCallbackNum, msgCount - 2);
                }

                // step 4: verify the connections that received messages
                var primary = counts.Where(c => c.Key.Endpoint.EndpointType == EndpointType.Primary);
                Assert.Single(primary);
                // the primary is the one we used to send client message
                Assert.Equal(primary.FirstOrDefault().Key, primarySvc0);
                // and it received N + 2 messages
                Assert.Equal(primary.FirstOrDefault().Value, MsgNum + 2);

                // for every secondary verify that
                // - their number equals to the number of seconary endpoints
                // - each received N + 2 messages
                // - each of the secondary ones is the same as the original selection
                var secondary = counts.Where(c => c.Key.Endpoint.EndpointType == EndpointType.Secondary);
                var secondaryEndpoints = MockServiceMessageOrderTestParams.ServiceEndpoints.Where(ep => ep.EndpointType == EndpointType.Secondary);
                Assert.Equal(secondary.Count(), secondaryEndpoints.Count());
                foreach (var secCon in secondary)
                {
                    // each of the new secondary connections received MsgNum + 2 (including initial 2 calls) messages
                    Assert.Equal(secCon.Value, MsgNum + 2);
                }
            }
        }

        [Fact]
        // Hub method is not flowing execution context but using ClientConnectionScope instead
        public async Task OutgoingMessagesWithoutExecutionContextFlow()
        {
            var builder = WebHost.CreateDefaultBuilder()
                 .ConfigureServices((IServiceCollection services) => { })
                 .ConfigureLogging(logging => logging.AddXunit(_output))
                 .UseStartup<IntegrationTestStartup<MockServiceMessageOrderTestParams, MessageOrderTestHub>>();

            using (var server = new AspNetTestServer(builder))
            {
                var mockSvc = (server.Host.Services.GetRequiredService<ServiceHubDispatcher<MessageOrderTestHub>>() as MockServiceHubDispatcher<MessageOrderTestHub>).MockService;
                await mockSvc.AllConnectionsEstablished();
                List<MockServiceSideConnection> allSvcConns = mockSvc.ServiceSideConnections;
                mockSvc.CurrentInvocationBinder = new TestHubBroadcastNCallsInvocationBinder();
                var priList = allSvcConns.Where(i => i.Endpoint.EndpointType == EndpointType.Primary).ToList();
                var primarySvc0 = priList[StaticRandom.Next(priList.Count)];
                var client0 = await primarySvc0.ConnectClientAsync();

                const int MsgNum = 10;
                await client0.SendMessage("BroadcastNumCallsNotFlowing", new object[] { MsgNum });

                var counts = new ConcurrentDictionary<MockServiceSideConnection, int>();
                int endpointCount = allSvcConns.Distinct(new MockServiceSideConnectionEndpointComparer()).Count();
                for (int ep = 0; ep < endpointCount * MsgNum; ep++)
                {
                    var connWithMessage = await Task.WhenAny(allSvcConns.Select(async c =>
                    {
                        bool moreData = await c.WaitToDequeueMessageAsync<BroadcastDataMessage>();
                        Assert.True(moreData);
                        return (c, moreData);
                    }));

                    var conn = connWithMessage.Result.c;
                    var newMsg = await conn.DequeueMessageAsync<BroadcastDataMessage>();

                    int msgCount = counts.GetOrAdd(conn, 0);
                    counts[conn] = ++msgCount;

                    var hubMessage = ParseBroadcastDataMessageJson(newMsg, mockSvc.CurrentInvocationBinder);
                    var invMsg = hubMessage as InvocationMessage;
                    Assert.Equal("Callback", invMsg.Target);

                    // verify the order of messages
                    int actualCallbackNum = (int)invMsg.Arguments[0];
                    Assert.Equal(actualCallbackNum, msgCount);
                }

                Assert.Equal(counts.Count(), MockServiceMessageOrderTestParams.ServiceEndpoints.Count());
                foreach (var conn in counts)
                {
                    Assert.Equal(conn.Value, MsgNum);
                }
            }
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
