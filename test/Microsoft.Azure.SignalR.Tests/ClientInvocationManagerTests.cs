// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET7_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
        private static readonly string SuccessCompleteResult = "success-result";
        private static readonly string ErrorCompleteResult = "error-result";

        private static ClientInvocationManager GetTestClientInvocationManager(int endpointCount = 1)
        {
            var services = new ServiceCollection();
            var endpoints = Enumerable.Range(0, endpointCount)
                .Select(i => new ServiceEndpoint($"Endpoint=https://test{i}connectionstring;AccessKey=1"))
                .ToArray();

            var config = new ConfigurationBuilder().Build();

            var serviceProvider = services.AddLogging()
                .AddSignalR().AddAzureSignalR(o => o.Endpoints = endpoints)
                .Services
                .AddSingleton<IConfiguration>(config)
                .BuildServiceProvider();

            var manager = serviceProvider.GetService<IServiceEndpointManager>();
            var endpointRouter = serviceProvider.GetService<IEndpointRouter>();

            var clientInvocationManager = new ClientInvocationManager(HubProtocolResolver, manager, endpointRouter);
            return clientInvocationManager;
        }

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
            var clientInvocationManager = GetTestClientInvocationManager();
            var connectionId = TestConnectionIds[0];
            var invocationId = clientInvocationManager.Caller.GenerateInvocationId(connectionId);

            CancellationToken cancellationToken = new CancellationToken();
            // Server A knows the InstanceId of Client 2, so `instaceId` in `AddInvocation` is `targetClientInstanceId` 
            var task = clientInvocationManager.Caller.AddInvocation<string>("TestHub", connectionId, invocationId, cancellationToken);

            var ret = clientInvocationManager.Caller.TryGetInvocationReturnType(invocationId, out var t);

            Assert.True(ret);
            Assert.Equal(typeof(string), t);

            var completionMessage = isCompletionWithResult
                ? CompletionMessage.WithResult(invocationId, SuccessCompleteResult)
                : CompletionMessage.WithError(invocationId, ErrorCompleteResult);

            ret = clientInvocationManager.Caller.TryCompleteResult(connectionId, completionMessage);
            Assert.True(ret);

            try
            {
                await task;
                Assert.True(isCompletionWithResult);
                Assert.Equal(SuccessCompleteResult, task.Result);
            }
            catch (Exception e)
            {
                Assert.False(isCompletionWithResult);
                Assert.Equal(ErrorCompleteResult, e.Message);
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
            var ciManagers = new ClientInvocationManager[] {
                GetTestClientInvocationManager(),
                GetTestClientInvocationManager()
            };
            var invocationId = ciManagers[0].Caller.GenerateInvocationId(TestConnectionIds[0]);

            CancellationToken cancellationToken = new CancellationToken();
            // Server 1 doesn't know the InstanceId of Client 2, so `instaceId` is null for `AddInvocation`
            var task = ciManagers[0].Caller.AddInvocation<string>("TestHub", TestConnectionIds[0], invocationId, cancellationToken);
            ciManagers[0].Caller.AddServiceMapping(new ServiceMappingMessage(invocationId, TestConnectionIds[1], TestInstanceIds[1]));
            ciManagers[1].Router.AddInvocation(TestConnectionIds[1], invocationId, serverIds[0], new CancellationToken());

            var completionMessage = isCompletionWithResult
                                ? CompletionMessage.WithResult(invocationId, SuccessCompleteResult)
                                : CompletionMessage.WithError(invocationId, ErrorCompleteResult);

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
                Assert.Equal(SuccessCompleteResult, task.Result);
            }
            catch (Exception e)
            {
                Assert.False(isCompletionWithResult);
                Assert.Equal(ErrorCompleteResult, e.Message);
            }
        }

        [Fact]
        public void TestCallerManagerCancellation()
        {
            var clientInvocationManager = GetTestClientInvocationManager();
            var invocationId = clientInvocationManager.Caller.GenerateInvocationId(TestConnectionIds[0]);
            var cts = new CancellationTokenSource();
            var task = clientInvocationManager.Caller.AddInvocation<string>("TestHub", TestConnectionIds[0], invocationId, cts.Token);

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


        [Theory]
        [InlineData(true, 2)]
        [InlineData(false, 2)]
        [InlineData(true, 3)]
        [InlineData(false, 3)]
        // isCompletionWithResult: the invocation is completed with result or error 
        public async void TestCompleteWithMultiEndpointAtLast(bool isCompletionWithResult, int endpointsCount)
        {
            Assert.True(endpointsCount > 1);
            var clientInvocationManager = GetTestClientInvocationManager(endpointsCount);
            var connectionId = TestConnectionIds[0];
            var invocationId = clientInvocationManager.Caller.GenerateInvocationId(connectionId);

            var cancellationToken = new CancellationToken();
            // Server A knows the InstanceId of Client 2, so `instaceId` in `AddInvocation` is `targetClientInstanceId` 
            var task = clientInvocationManager.Caller.AddInvocation<string>("TestHub", connectionId, invocationId, cancellationToken);

            var ret = clientInvocationManager.Caller.TryGetInvocationReturnType(invocationId, out var t);

            Assert.True(ret);
            Assert.Equal(typeof(string), t);

            var completionMessage = CompletionMessage.WithResult(invocationId, SuccessCompleteResult);
            var errorCompletionMessage = CompletionMessage.WithError(invocationId, ErrorCompleteResult);

            // The first `endpointsCount - 1` CompletionMessage complete the invocation with error
            // The last one completes the invocation according to `isCompletionWithResult`
            // The invocation should be uncompleted until the last one CompletionMessage
            for (var i = 0; i < endpointsCount - 1; i++)
            {
                var currentCompletionMessage = errorCompletionMessage;
                ret = clientInvocationManager.Caller.TryCompleteResult(connectionId, currentCompletionMessage);
                Assert.False(ret);
            }

            ret = clientInvocationManager.Caller.TryCompleteResult(connectionId, isCompletionWithResult ? completionMessage : errorCompletionMessage);
            Assert.True(ret);

            try
            {
                await task;
                Assert.True(isCompletionWithResult);
                Assert.Equal(SuccessCompleteResult, task.Result);
            }
            catch (Exception e)
            {
                Assert.False(isCompletionWithResult);
                Assert.Equal(ErrorCompleteResult, e.Message);
            }
        }

        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        public async void TestCompleteWithMultiEndpointAtMiddle(int endpointsCount)
        {
            Assert.True(endpointsCount > 1);
            var clientInvocationManager = GetTestClientInvocationManager(endpointsCount);
            var connectionId = TestConnectionIds[0];
            var invocationId = clientInvocationManager.Caller.GenerateInvocationId(connectionId);

            var cancellationToken = new CancellationToken();
            // Server A knows the InstanceId of Client 2, so `instaceId` in `AddInvocation` is `targetClientInstanceId` 
            var task = clientInvocationManager.Caller.AddInvocation<string>("TestHub", connectionId, invocationId, cancellationToken);

            var ret = clientInvocationManager.Caller.TryGetInvocationReturnType(invocationId, out var t);

            Assert.True(ret);
            Assert.Equal(typeof(string), t);

            var successCompletionMessage = CompletionMessage.WithResult(invocationId, SuccessCompleteResult);
            var errorCompletionMessage = CompletionMessage.WithError(invocationId, ErrorCompleteResult);

            // The first `endpointsCount - 2` CompletionMessage complete the invocation with error
            // The next one completes the invocation with result
            // The last one completes the invocation with error and it shouldn't change the invocation result
            for (var i = 0; i < endpointsCount - 2; i++)
            {
                ret = clientInvocationManager.Caller.TryCompleteResult(connectionId, errorCompletionMessage);
                Assert.False(ret);
            }

            ret = clientInvocationManager.Caller.TryCompleteResult(connectionId, successCompletionMessage);
            Assert.True(ret);

            ret = clientInvocationManager.Caller.TryCompleteResult(connectionId, errorCompletionMessage);
            Assert.False(ret);

            try
            {
                await task;
                Assert.Equal(SuccessCompleteResult, task.Result);
            }
            catch (Exception)
            {
                Assert.True(false);
            }
        }

        internal static ReadOnlyMemory<byte> GetBytes(string proto, HubMessage message)
        {
            IHubProtocol hubProtocol = proto == "json" ? new JsonHubProtocol() : new MessagePackHubProtocol();
            return hubProtocol.GetMessageBytes(message);
        }

    }
}
#endif