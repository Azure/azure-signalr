// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class StronglyTypedServiceHubContextFacts
    {
        private readonly ILoggerFactory _loggerFactory = new LoggerFactory();

        public StronglyTypedServiceHubContextFacts(ITestOutputHelper testOutputHelper)
        {
            _loggerFactory.AddXunit(testOutputHelper);
        }

        #region Test CreateAndDispose
        [Theory]
        [InlineData(ServiceTransportType.Persistent)]
        [InlineData(ServiceTransportType.Transient)]
        public async Task TestCreateAndDisposeServiceHubContext(ServiceTransportType serviceTransportType)
        {
            using var serviceHubContextToDisposeSync = await Create(serviceTransportType);
        }

        [Theory]
        [InlineData(ServiceTransportType.Persistent)]
        [InlineData(ServiceTransportType.Transient)]
        public async Task TestCreateAndDisposeAsyncServiceHubContext(ServiceTransportType serviceTransportType)
        {
            await using var serviceHubContextToDisposeAsync = await Create(serviceTransportType);
        }
        #endregion

        [Theory]
        [InlineData(ServiceTransportType.Persistent)]
        [InlineData(ServiceTransportType.Transient)]
        public async Task TestNegotiateAsync(ServiceTransportType serviceTransportType)
        {
            await using var hubContext = await Create(serviceTransportType);
            var negotaiteResponse = await hubContext.NegotiateAsync();
            Assert.NotNull(negotaiteResponse);
            Assert.NotNull(negotaiteResponse.AccessToken);
            Assert.NotNull(negotaiteResponse.Url);
        }

        #region Test IHubContext<T>
        [Fact]
        public async Task TestTransientBroadcastMessage()
        {
            var messageContext = "Hello World";
            void assertion(HttpRequestMessage request, CancellationToken t)
            {
                var payload = new PayloadMessage { Target = nameof(IChat.NewMessage), Arguments = new[] { messageContext } };
                var actual = request.Content.ReadAsStringAsync().Result;
                var expected = JsonConvert.SerializeObject(payload);
                Assert.Equal(expected, actual);
            }
            var services = new ServiceCollection().AddHttpClient(Options.DefaultName)
                .ConfigurePrimaryHttpMessageHandler(() => new TestRootHandler(assertion)).Services
                .AddSignalRServiceManager();
            await using var hubContext = await Create(ServiceTransportType.Transient, services);

            _ = hubContext.Clients.All.NewMessage(messageContext);
        }

        [Fact]
        public async Task TestPersistentBroadcastMessage()
        {
            var messageContext = "Hello World";
            var connectFactory = new TestServiceConnectionFactory();
            var serviceManager = new ServiceManagerBuilder()
                .WithOptions(o =>
                {
                    o.ServiceTransportType = ServiceTransportType.Persistent;
                    o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).First();
                })
                .ConfigureServices(services => services.AddSingleton<IServiceConnectionFactory>(connectFactory))
                .WithLoggerFactory(_loggerFactory)
                .BuildServiceManager();
            await using var hubContext = await serviceManager.CreateHubContextAsync<IChat>("hubName", default);
            _ = hubContext.Clients.All.NewMessage(messageContext);

            var message = connectFactory.CreatedConnections.First().Value.SelectMany(c => (c as TestServiceConnection).ReceivedMessages).Single();
            var broadcastMessage = Assert.IsType<BroadcastDataMessage>(message);

            var payload = Serialize(hubContext, nameof(IChat.NewMessage), new object[] { messageContext }, out var protocolName);
            var actualPayload = broadcastMessage.Payloads[protocolName];
            Assert.True(payload.Span.SequenceEqual(actualPayload.Span));
        }

        #endregion

        #region Test GroupManager
        [Fact]
        public async Task TestTransientAddConnectionToGroup()
        {
            var connectionId = "connectionid";
            var groupName = "groupName";
            void assertion(HttpRequestMessage request, CancellationToken t)
            {
                Assert.EndsWith($"/groups/{groupName}/connections/{connectionId}", request.RequestUri.AbsoluteUri);
            }
            var services = new ServiceCollection().AddHttpClient(Options.DefaultName)
                .ConfigurePrimaryHttpMessageHandler(() => new TestRootHandler(assertion)).Services
                .AddSignalRServiceManager();
            await using var hubContext = await Create(ServiceTransportType.Transient, services);

            await hubContext.Groups.AddToGroupAsync(connectionId, groupName);
        }

        [Fact]
        public async Task TestPersistentAddConnectionToGroup()
        {
            var connectionId = "connectionid";
            var groupName = "groupName";
            var connectFactory = new TestServiceConnectionFactory();
            var serviceManager = new ServiceManagerBuilder()
                .WithOptions(o =>
                {
                    o.ServiceTransportType = ServiceTransportType.Persistent;
                    o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).First();
                })
                .ConfigureServices(services => services.AddSingleton<IServiceConnectionFactory>(connectFactory))
                .WithLoggerFactory(_loggerFactory)
                .BuildServiceManager();
            await using var hubContext = await serviceManager.CreateHubContextAsync<IChat>("hubName", default);
            //Don't wait for ack
            _ = hubContext.Groups.AddToGroupAsync(connectionId, groupName);

            var messages = connectFactory.CreatedConnections.First().Value.SelectMany(c => (c as TestServiceConnection).ReceivedMessages);
            Assert.Contains(messages, m => m is JoinGroupWithAckMessage ackMessage && ackMessage.ConnectionId == connectionId && ackMessage.GroupName == groupName);
        }
        #endregion

        #region Test UserGroupManager
        [Fact]
        public async Task TestTransientAddUserToGroup()
        {
            var userId = "userId";
            var groupName = "groupName";
            void assertion(HttpRequestMessage request, CancellationToken t)
            {
                Assert.EndsWith($"/groups/{groupName}/users/{userId}", request.RequestUri.AbsoluteUri);
            }
            var services = new ServiceCollection().AddHttpClient(Options.DefaultName)
                .ConfigurePrimaryHttpMessageHandler(() => new TestRootHandler(assertion)).Services
                .AddSignalRServiceManager();
            await using var hubContext = await Create(ServiceTransportType.Transient, services);

            await hubContext.UserGroups.AddToGroupAsync(userId, groupName);
        }

        [Fact]
        public async Task TestPersistentAddUserToGroup()
        {
            var userId = "user";
            var groupName = "groupName";
            var connectFactory = new TestServiceConnectionFactory();
            var serviceManager = new ServiceManagerBuilder()
                .WithOptions(o =>
                {
                    o.ServiceTransportType = ServiceTransportType.Persistent;
                    o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).First();
                })
                .ConfigureServices(services => services.AddSingleton<IServiceConnectionFactory>(connectFactory))
                .WithLoggerFactory(_loggerFactory)
                .BuildServiceManager();
            await using var hubContext = await serviceManager.CreateHubContextAsync<IChat>("hubName", default);
            //Don't wait for ack
            _ = hubContext.UserGroups.AddToGroupAsync(userId, groupName);

            var messages = connectFactory.CreatedConnections.First().Value.SelectMany(c => (c as TestServiceConnection).ReceivedMessages);
            Assert.Contains(messages, m => m is UserJoinGroupWithAckMessage ackMessage && ackMessage.UserId == userId && ackMessage.GroupName == groupName);
        }
        #endregion

        #region Test ClientManager
        [Fact]
        public async Task TestTransientCloseConnection()
        {
            var connectionId = "connectionId";
            void assertion(HttpRequestMessage request, CancellationToken t)
            {
                Assert.EndsWith($"/connections/{connectionId}", request.RequestUri.AbsoluteUri);
                Assert.Equal(HttpMethod.Delete, request.Method);
            }
            var services = new ServiceCollection().AddHttpClient(Options.DefaultName)
                .ConfigurePrimaryHttpMessageHandler(() => new TestRootHandler(assertion)).Services
                .AddSignalRServiceManager();
            await using var hubContext = await Create(ServiceTransportType.Transient, services);

            await hubContext.ClientManager.CloseConnectionAsync(connectionId);
        }

        [Fact]
        public async Task TestPersistentCloseConnection()
        {
            var connectionId = "connectionId";
            var connectFactory = new TestServiceConnectionFactory();
            var serviceManager = new ServiceManagerBuilder()
                .WithOptions(o =>
                {
                    o.ServiceTransportType = ServiceTransportType.Persistent;
                    o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).First();
                })
                .ConfigureServices(services => services.AddSingleton<IServiceConnectionFactory>(connectFactory))
                .WithLoggerFactory(_loggerFactory)
                .BuildServiceManager();
            await using var hubContext = await serviceManager.CreateHubContextAsync<IChat>("hubName", default);
            _ = hubContext.ClientManager.CloseConnectionAsync(connectionId);

            var messages = connectFactory.CreatedConnections.First().Value.SelectMany(c => (c as TestServiceConnection).ReceivedMessages);
            Assert.Contains(messages, m => m is CloseConnectionMessage message && message.ConnectionId == connectionId);

        }
        #endregion

        public interface IChat
        {
            public Task NewMessage(string message);
        }

        private Task<ServiceHubContext<IChat>> Create(ServiceTransportType transportType, IServiceCollection services = null)
        {
            var builder = services == null ? new ServiceManagerBuilder() : new ServiceManagerBuilder(services);
            return builder.WithOptions(o =>
            {
                o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).Single();
                o.ServiceTransportType = transportType;
            })
            .WithLoggerFactory(_loggerFactory)
            .BuildServiceManager()
            .CreateHubContextAsync<IChat>("hubName", default);
        }

        private static ReadOnlyMemory<byte> Serialize(ServiceHubContext<IChat> hubContext, string methodName, object[] args, out string protocolName)
        {
            var hubProtocol = (hubContext as ServiceHubContextImpl<IChat>).ServiceProvider.GetRequiredService<IHubProtocolResolver>().GetProtocol("json", null);
            protocolName = hubProtocol.Name;
            var message = new InvocationMessage(methodName, args);
            return hubProtocol.GetMessageBytes(message);
        }
    }
}