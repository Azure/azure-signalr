// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET7_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.Azure.SignalR
{
    public class ClientInvocationManagerTests
    {
        private static readonly IHubProtocolResolver HubProtocolResolver =
            new DefaultHubProtocolResolver(new IHubProtocol[]
            {
                        new JsonHubProtocol(),
                        new MessagePackHubProtocol()
            },
            NullLogger<DefaultHubProtocolResolver>.Instance
        );

        private static readonly List<string> TestConnectionIds = new() { "conn0", "conn1" };
        private static readonly List<string> TestInstanceIds = new() { "instance0", "instance1" };
        private static readonly List<string> TestServerIds = new() { "server1", "server2" };

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        /*
         * Client 1 <-->  ---------
         *                | Pod 1 | <--> Server A
         * Client 2 <-->  ---------
         * 
         * Note: Client 1 and Client 2 are both managed by Server A
         */
        public async void TestCompleteWithoutRouterServer(bool isCompletionWithResult)
        {
            var connectionId = TestConnectionIds[0];
            var targetClientInstanceId = TestInstanceIds[0];
            var clientInvocationManager = new ClientInvocationManager(HubProtocolResolver);
            var invocationId = clientInvocationManager.Caller.GenerateInvocationId(connectionId);
            var invocationResult = "invocation-correct-result";

            CancellationToken cancellationToken = new CancellationToken();
            // Server A knows the InstanceId of Client 2, so `instaceId` in `AddInvocation` is `targetClientInstanceId` 
            var task = clientInvocationManager.Caller.AddInvocation<string>(connectionId, invocationId, cancellationToken);

            var ret = clientInvocationManager.Caller.TryGetInvocationReturnType(invocationId, out var t);

            Assert.True(ret);
            Assert.Equal(typeof(string), t);

            var completionMessage = isCompletionWithResult
                ? CompletionMessage.WithResult(invocationId, invocationResult)
                : CompletionMessage.WithError(invocationId, invocationResult);

            ret = clientInvocationManager.Caller.TryCompleteResult(connectionId, completionMessage);
            Assert.True(ret);

            try
            {
                await task;
                Assert.True(isCompletionWithResult);
                Assert.Equal(invocationResult, task.Result);
            }
            catch (Exception e)
            {
                Assert.False(isCompletionWithResult);
                Assert.Equal(invocationResult, e.Message);
            }
        }

        [Theory]
        [InlineData("json", true)]
        [InlineData("json", false)]
        [InlineData("messagepack", true)]
        [InlineData("messagepack", false)]
        /*                           ---------  <--> Client 2
         * Server 1 <--> Pod 1 <-->  | Pod 2 |
         *                           ---------  <--> Server 2       
         * 
         * Note: Server 2 manages Client 2.
         */
        public async void TestCompleteWithRouterServer(string protocol, bool isCompletionWithResult)
        {
            var serverIds = new string[] { TestServerIds[0], TestServerIds[1] };
            var invocationResult = "invocation-correct-result";
            var ciManagers = new ClientInvocationManager[]
            {
                new ClientInvocationManager(HubProtocolResolver),
                new ClientInvocationManager(HubProtocolResolver),
            };
            var invocationId = ciManagers[0].Caller.GenerateInvocationId(TestConnectionIds[0]);

            CancellationToken cancellationToken = new CancellationToken();
            // Server 1 doesn't know the InstanceId of Client 2, so `instaceId` is null for `AddInvocation`
            var task = ciManagers[0].Caller.AddInvocation<string>(TestConnectionIds[0], invocationId, cancellationToken);
            ciManagers[0].Caller.AddServiceMapping(new ServiceMappingMessage(invocationId, TestConnectionIds[1], TestInstanceIds[1]));
            ciManagers[1].Router.AddInvocation(TestConnectionIds[1], invocationId, serverIds[0], new CancellationToken());

            var completionMessage = isCompletionWithResult
                                ? CompletionMessage.WithResult(invocationId, invocationResult)
                                : CompletionMessage.WithError(invocationId, invocationResult);

            var ret = ciManagers[1].Router.TryCompleteResult(TestConnectionIds[1], completionMessage);
            Assert.True(ret);

            var payload = GetBytes(protocol, completionMessage);
            var clientCompletionMessage = new ClientCompletionMessage(invocationId, TestConnectionIds[0], serverIds[1], protocol, payload);

            ret = ciManagers[0].Caller.TryCompleteResult(clientCompletionMessage.ConnectionId, clientCompletionMessage);
            Assert.True(ret);

            try
            {
                await task;
                Assert.True(isCompletionWithResult);
                Assert.Equal(invocationResult, task.Result);
            }
            catch (Exception e)
            {
                Assert.False(isCompletionWithResult);
                Assert.Equal(invocationResult, e.Message);
            }
        }

        [Fact]
        public void TestCallerManagerCancellation()
        {
            var clientInvocationManager = new ClientInvocationManager(HubProtocolResolver);
            var invocationId = clientInvocationManager.Caller.GenerateInvocationId(TestConnectionIds[0]);
            var cts = new CancellationTokenSource();
            var task = clientInvocationManager.Caller.AddInvocation<string>(TestConnectionIds[0], invocationId,  cts.Token);

            // Check if the invocation is existing
            Assert.True(clientInvocationManager.Caller.TryGetInvocationReturnType(invocationId, out _));
            // Cancel the invocation by CancellationToken
            cts.Cancel(true);
            // Check if the invocation task has the information
            Assert.Equal("One or more errors occurred. (Canceled)", task.Exception.Message);
            Assert.True(task.IsFaulted);
            // Check if the invocation was removed
            Assert.False(clientInvocationManager.Caller.TryGetInvocationReturnType(invocationId, out _));
        }

        internal static ReadOnlyMemory<byte> GetBytes(string proto, HubMessage message)
        {
            IHubProtocol hubProtocol = proto == "json" ? new JsonHubProtocol() : new MessagePackHubProtocol();
            return hubProtocol.GetMessageBytes(message);
        }

    }
}
#endif