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
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using AspNetTestServer = Microsoft.AspNetCore.TestHost.TestServer;

namespace Microsoft.Azure.SignalR.IntegrationTests
{

    public class MessageOrderTests : VerifiableLoggedTest
    {
        const bool MessageOrderFixed = false;
        private ITestOutputHelper _output;

        public MessageOrderTests(ITestOutputHelper output) : base(output)
        {
            _output = output;
        }

        [Fact]
        // verifies the outgoing messages from the hub call:
        // * are using the same primary service connection the hub call was made from
        // * pick and stick to the same secondary connection
        public async Task OutgoingMessagesUseSameServiceConnection()
        {
            var builder = WebHost.CreateDefaultBuilder()
                .ConfigureServices((IServiceCollection services) =>{})
                .ConfigureLogging(logging => logging.AddXunit(_output))
                .UseStartup<IntegrationTestStartup<RealMockServiceE2ETestParams, TestHub>>();

            using(var server = new AspNetTestServer(builder))
            {
                var mockSvc = (server.Host.Services.GetRequiredService<ServiceHubDispatcher<TestHub>>() as MockServiceHubDispatcher<TestHub>).MockService;
                List<MockServiceSideConnection> allSvcConns = null;
                int endpointCount = 0;

                // wait for all connections to get established
                // todo: extension/utility method?
                do 
                {
                    await Task.Delay(10);
                    allSvcConns = mockSvc.ServiceSideConnections;
                    // count endpoints (primary and secondary)
                    endpointCount = allSvcConns.Distinct(new MockServiceSideConnectionEndpointComparer()).Count();
                }
                while (endpointCount != RealMockServiceE2ETestParams.ServiceEndpoints.Length);

                // specify invocation binder before making calls
                // todo: maybe there is a better way?
                mockSvc.CurrentInvocationBinder = new TestHubBroadcastNCallsInvocationBinder();

                // use the first primary to make a client connection
                var primarySvc0 = allSvcConns.Where(i => i.Endpoint.EndpointType == EndpointType.Primary).FirstOrDefault();
                var client0 = await primarySvc0.ConnectClientAsync();

                const int MsgNum = 10;
                await client0.SendMessage("BroadcastNumCalls", new object[] { MsgNum });

                // The mock service does not route messages to client connections as there are no actual clients connected
                // So we just ask for raw messages received by the mock service and count them per service connection
                // Each endpoint will get the broadcast message so we should receive endpointCount * MsgNum messages
                var counts = new ConcurrentDictionary<MockServiceSideConnection, int>();

                for (int ep = 0; ep < endpointCount * MsgNum ; ep++)
                {
                    // todo: extension method?
                    var receivedMessage = await Task.WhenAny(allSvcConns.Select(async c =>
                    {
                        var msg = await c.PeekMessageAsync<BroadcastDataMessage>();
                        return (c, msg);
                    }));

                    var conn = receivedMessage.Result.c;
                    var peekMsg = receivedMessage.Result.msg;
                    var dqMsg = await conn.DequeueMessageAsync<BroadcastDataMessage>();
                    Assert.Equal(peekMsg, dqMsg);

                    int msgCount = counts.GetOrAdd (conn, 0);
                    counts[conn] = ++msgCount;

                    // parse each BroadcastDataMessage and verify this is the correct message
                    var hubMessage = ParseBrDaMeJson(peekMsg, mockSvc.CurrentInvocationBinder);
                    Assert.True(hubMessage is InvocationMessage);
                    var invMsg = hubMessage as InvocationMessage;
                    Assert.True(invMsg.Target == "Callback");

                    // finally, get ready to verify the order of messages
                    int actualCallbackNum = (int)invMsg.Arguments[0];

                    if (MessageOrderFixed)
                    {
                        // this check works for both primary and secondary connections
                        Assert.Equal(actualCallbackNum, msgCount);
                    }
                }

                // todo: verify we received no extra BroadcastDataMessage - need TryPeek method (async with timeout?)

                if (MessageOrderFixed)
                {
                    // Did we got the expected number of calls and all of them stick to exactly one primary and one secondary?
                    var primary = counts.Where(c => c.Key.Endpoint.EndpointType == EndpointType.Primary);

                    Assert.Single(primary);

                    // and the primary is the one we used to send client message
                    Assert.Equal(primary.FirstOrDefault().Key, primarySvc0);

                    // and it received N messages
                    Assert.Equal(primary.FirstOrDefault().Value, MsgNum);

                    // for every secondary verify that
                    // - their number equals to the number of seconary endpoints
                    // - each received N messages
                    var secondary = counts.Where(c => c.Key.Endpoint.EndpointType == EndpointType.Secondary);
                    Assert.Single(secondary);
                    Assert.Equal(secondary.FirstOrDefault().Value, MsgNum);
                }
            }
        }

        private static readonly JsonHubProtocol _signalRPro = new JsonHubProtocol();
        private static HubMessage ParseBrDaMeJson(BroadcastDataMessage bdm, IInvocationBinder binder)
        {
            foreach (var payload in bdm.Payloads)
            {
                if (payload.Key == "json")
                {
                    var sequence = new ReadOnlySequence<byte>(payload.Value);
                    if (_signalRPro.TryParseMessage(ref sequence, binder, out var signalRRRmessage))
                    {
                        if (signalRRRmessage is InvocationMessage inv)
                        {
                            Console.WriteLine($" -- {bdm}: callback#  {inv.Target} {inv.Arguments[0]}");
                        }
                        else
                        {
                            Console.WriteLine($"oh something else {signalRRRmessage}");
                        }
                        return signalRRRmessage;
                    }
                    else
                    {
                        Console.WriteLine("failed..");
                    }
                }
                else
                {
                    if (bdm.Payloads.Count == 1)
                    {
                        // this is not expected
                        Console.WriteLine($"unexpected number of payloads {payload.Key}");
                    }
                }
            }
            return null;
        }
    }
}
