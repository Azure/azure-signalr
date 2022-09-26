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
        // Server 1 <--> Pod 1 <--> Client 2
        public async void TestNormalCompleteWithoutRouterServer()
        {
            var serverGUID = Guid.NewGuid().ToString();
            var connectionId = "Connection-0";
            var invocationResult = "invocation-success-result";
            ClientInvocationManager clientInvocationManager = new ClientInvocationManager(HubProtocolResolver);
            var invocationId = clientInvocationManager.Caller.GetNewInvocationId(connectionId);

            CancellationToken cancellationToken = new CancellationToken();
            var task = clientInvocationManager.Caller.AddInvocation<string>(connectionId, invocationId, cancellationToken);

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
        //                           ---------  <--> Client 2
        // Server 1 <--> Pod 1 <-->  | Pod 2 |
        //                           ---------  <--> Server 2       
        public async void TestNormalCompleteWithRouterServer(string protocol)
        {
            var serversGUID = new string[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() };
            var instancesId = new string[] { "Instance-0", "Instance-1" };
            var callerServerId = "Server-0";
            var connectionsId = new string[] { "Connection-0", "Connection-1" };
            var invocationResult = "invocation-success-result";
            var ciManagers = new ClientInvocationManager[]
            {
                new ClientInvocationManager(HubProtocolResolver),
                new ClientInvocationManager(HubProtocolResolver),
            };
            var invocationId = ciManagers[0].Caller.GetNewInvocationId(connectionsId[0]);
            var completionMessage = new CompletionMessage(invocationId, null, invocationResult, true);

            CancellationToken cancellationToken = new CancellationToken();
            var task = ciManagers[0].Caller.AddInvocation<string>(connectionsId[0], invocationId, cancellationToken);
            ciManagers[0].Caller.AddServiceMappingMessage(new ServiceMappingMessage(invocationId, connectionsId[1], instancesId[1]));
            ciManagers[1].Router.AddRoutedInvocation(connectionsId[1], invocationId, callerServerId, instancesId[1], new CancellationToken());

            var ret = ciManagers[1].Router.TryCompleteResult(connectionsId[1], completionMessage);
            Assert.True(ret);

            var payload = GetBytes(protocol, completionMessage);
            var clientCompletionMessage = new ClientCompletionMessage(invocationId, connectionsId[0], callerServerId, protocol, payload);

            ret = ciManagers[0].Caller.TryCompleteResult(clientCompletionMessage.ConnectionId, clientCompletionMessage.Protocol, clientCompletionMessage.Payload);
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