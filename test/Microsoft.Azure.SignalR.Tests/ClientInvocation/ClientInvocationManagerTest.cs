// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET7_0_OR_GREATER
using System;
using System.Dynamic;
using System.Threading;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests
{
    public class ClientInvocationManagerTest
    {
        private static readonly IHubProtocolResolver HubProtocolResolver =
            new DefaultHubProtocolResolver(new IHubProtocol[]
            {
                        new JsonHubProtocol(),
                        new MessagePackHubProtocol()
            },
            NullLogger<DefaultHubProtocolResolver>.Instance
        );

        [Fact]
        /*
         * Client 1 <-->  ---------
         *                | Pod 1 | <--> Server A
         * Client 2 <-->  ---------
         * 
         * Note: Client 1 and Client 2 are both managed by Server A
         */
        public async void TestNormalCompleteWithoutRouterServer()
        {
            var connectionId = "Connection-0";
            var invocationResult = "invocation-success-result";
            var targetClientInstanceId = "Instance 1";
            ClientInvocationManager clientInvocationManager = new ClientInvocationManager(HubProtocolResolver);
            var invocationId = clientInvocationManager.Caller.GenerateInvocationId(connectionId);

            CancellationToken cancellationToken = new CancellationToken();
            // Server A knows the InstanceId of Client 2, so `instaceId` in `AddInvocation` is `targetClientInstanceId` ("Instance 1")
            var task = clientInvocationManager.Caller.AddInvocation<string>(connectionId, invocationId, targetClientInstanceId, cancellationToken);

            var ret = clientInvocationManager.Caller.TryGetInvocationReturnType(invocationId, out Type T);

            Assert.True(ret);
            Assert.Equal(typeof(string), T);

            var completionMessage = new CompletionMessage(invocationId, null, invocationResult, true);
            ret = clientInvocationManager.Caller.TryCompleteResult(connectionId, completionMessage);
            Assert.True(ret);

            await task;
            Assert.Equal(invocationResult, task.Result);
        }

        [Theory]
        [InlineData("json")]
        [InlineData("messagepack")]
        /*                           ---------  <--> Client 2
         * Server 1 <--> Pod 1 <-->  | Pod 2 |
         *                           ---------  <--> Server 2       
         * 
         * Note: Server 2 manages Client 2.
         */
        public async void TestNormalCompleteWithRouterServer(string protocol)
        {
            var instanceIds = new string[] { "Instance-0", "Instance-1" };
            var serverIds = new string[] { "Server-0", "Server-1" };
            var connectionIds = new string[] { "Connection-0", "Connection-1" };
            var invocationResult = "invocation-success-result";
            var ciManagers = new ClientInvocationManager[]
            {
                new ClientInvocationManager(HubProtocolResolver),
                new ClientInvocationManager(HubProtocolResolver),
            };
            var invocationId = ciManagers[0].Caller.GenerateInvocationId(connectionIds[0]);
            var completionMessage = new CompletionMessage(invocationId, null, invocationResult, true);

            CancellationToken cancellationToken = new CancellationToken();
            // Server 1 doesn't know the InstanceId of Client 2, so `instaceId` is null for `AddInvocation`
            var task = ciManagers[0].Caller.AddInvocation<string>(connectionIds[0], invocationId, null, cancellationToken);
            ciManagers[0].Caller.AddServiceMapping(new ServiceMappingMessage(invocationId, connectionIds[1], instanceIds[1]));
            ciManagers[1].Router.AddInvocation(connectionIds[1], invocationId, serverIds[0], instanceIds[1], new CancellationToken());

            var ret = ciManagers[1].Router.TryCompleteResult(connectionIds[1], completionMessage);
            Assert.True(ret);

            var payload = GetBytes(protocol, completionMessage);
            var clientCompletionMessage = new ClientCompletionMessage(invocationId, connectionIds[0], serverIds[1], protocol, payload);

            ret = ciManagers[0].Caller.TryCompleteResult(clientCompletionMessage.ConnectionId, clientCompletionMessage);
            Assert.True(ret);

            await task;

            Assert.Equal(invocationResult, task.Result);
        }

        internal static ReadOnlyMemory<byte> GetBytes(string proto, HubMessage message)
        {
            IHubProtocol hubProtocol = proto == "json" ? new JsonHubProtocol() : new MessagePackHubProtocol();
            return hubProtocol.GetMessageBytes(message);
        }

    }
}
#endif