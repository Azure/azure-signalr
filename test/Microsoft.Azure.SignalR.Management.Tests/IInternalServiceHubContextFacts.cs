// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class IInternalServiceHubContextFacts : VerifiableLoggedTest
    {
        private const string Hub = nameof(Hub);
        private const string UserId = "User";
        private const string GroupName = "Group";
        private const int Count = 3;
        private static readonly ServiceEndpoint[] ServiceEndpoints = FakeEndpointUtils.GetFakeEndpoint(Count).ToArray();

        public IInternalServiceHubContextFacts(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task CreateServiceHubContext_WithReferenceNotEqualEndpoints()
        {
            //prepare endpoints
            var totalCount = 3;
            var selectedCount = 2;
            var endpoints = FakeEndpointUtils.GetFakeEndpoint(totalCount).ToArray();
            var targetEndpoints = endpoints.Take(selectedCount).Select(endpoint => new ServiceEndpoint(endpoint));

            //create services
            var services = new ServiceCollection().AddSignalRServiceManager()
                .Configure<ServiceManagerOptions>(o =>
                {
                    o.ServiceEndpoints = endpoints;
                    o.ServiceTransportType = ServiceTransportType.Persistent;
                });
            services.AddSingleton<IReadOnlyCollection<ServiceDescriptor>>(services.ToList());
            var serviceManager = services.BuildServiceProvider().GetRequiredService<IServiceManager>();

            var hubContext = (await serviceManager.CreateHubContextAsync(Hub) as IInternalServiceHubContext)
                .WithEndpoints(targetEndpoints);
            var serviceProvider = (hubContext as ServiceHubContextImpl).ServiceProvider;
            var container = serviceProvider.GetRequiredService<IServiceConnectionContainer>() as MultiEndpointMessageWriter;
            var innerEndpoints = container.TargetEndpoints.ToArray();
            var hubEndpoints = (hubContext as ServiceHubContextImpl).ServiceProvider.GetRequiredService<IServiceEndpointManager>().GetEndpoints(Hub);
            Assert.True(innerEndpoints.SequenceEqual(hubEndpoints.Take(selectedCount), ReferenceEqualityComparer.Instance));
        }

        [InlineData(ServiceTransportType.Persistent)]
        [Theory]
        public async Task Call_NegotiateAsync_After_WithEndpoints(ServiceTransportType serviceTransportType)
        {
            var serviceManager = new ServiceManagerBuilder()
                .WithOptions(o =>
                {
                    o.ServiceTransportType = serviceTransportType;
                    o.ServiceEndpoints = ServiceEndpoints;
                })
                .BuildServiceManager();
            var hubContext = await serviceManager.CreateHubContextAsync(Hub, default);
            for (var i = 0; i < 5; i++)
            {
                var randomEndpoint = ServiceEndpoints[StaticRandom.Next(0, Count)];
                var negotiationResponse = await (hubContext as IInternalServiceHubContext)
                    .WithEndpoints(new ServiceEndpoint[] { randomEndpoint })
                    .NegotiateAsync();

                Assert.Equal(ClientEndpointUtils.GetExpectedClientEndpoint(Hub, null, randomEndpoint.Endpoint), negotiationResponse.Url);
                var tokenString = negotiationResponse.AccessToken;
                var token = JwtTokenHelper.JwtHandler.ReadJwtToken(tokenString);
                var expectedToken = JwtTokenHelper.GenerateJwtBearer(
                    ClientEndpointUtils.GetExpectedClientEndpoint(Hub, null, randomEndpoint.Endpoint),
                    ClaimsUtility.BuildJwtClaims(null, null, null), token.ValidTo, token.ValidFrom, token.ValidFrom, randomEndpoint.AccessKey);
                Assert.Equal(expectedToken, tokenString);
            }
        }

        [InlineData(ServiceTransportType.Persistent)]
        [Theory]
        public async Task UserJoinGroup_Test(ServiceTransportType serviceTransportType)
        {
            Task testAction(ServiceHubContext hubContext)
            {
                // no need to wait for ack
                _ = hubContext.UserGroups.AddToGroupAsync(UserId, GroupName).OrTimeout(300);
                return Task.CompletedTask;
            }
            void assertAction(Dictionary<HubServiceEndpoint, List<TestServiceConnection>> createdConnections)
            {
                foreach (var list in createdConnections.Values)
                {
                    var msg = (UserJoinGroupWithAckMessage)list.SelectMany(l => l.ReceivedMessages).Single();
                    Assert.Equal(UserId, msg.UserId);
                    Assert.Equal(GroupName, msg.GroupName);
                }
            }

            await MockConnectionTestAsync(serviceTransportType, testAction, assertAction);
        }

        [InlineData(ServiceTransportType.Persistent)]
        [Theory]
        public async Task UserJoinGroupWithTTL_Test(ServiceTransportType serviceTransportType)
        {
            var ttl = TimeSpan.FromSeconds(1);

            Task testAction(ServiceHubContext hubContext) => hubContext.UserGroups.AddToGroupAsync(UserId, GroupName, ttl);

            void assertAction(Dictionary<HubServiceEndpoint, List<TestServiceConnection>> createdConnections)
            {
                foreach (var list in createdConnections.Values)
                {
                    var msg = (UserJoinGroupMessage)list.SelectMany(l => l.ReceivedMessages).Single();
                    Assert.Equal(UserId, msg.UserId);
                    Assert.Equal(GroupName, msg.GroupName);
                    Assert.Equal((int)ttl.TotalSeconds, msg.Ttl);
                }
            }

            await MockConnectionTestAsync(serviceTransportType, testAction, assertAction);
        }

        [InlineData(ServiceTransportType.Persistent)]
        [Theory]
        public async Task UserLeaveGroup_Test(ServiceTransportType serviceTransportType)
        {
            var userId = "User";
            var group = "Group";

            Task testAction(ServiceHubContext hubContext) => hubContext.UserGroups.RemoveFromGroupAsync(userId, group);

            void assertAction(Dictionary<HubServiceEndpoint, List<TestServiceConnection>> createdConnections)
            {
                foreach (var list in createdConnections.Values)
                {
                    var msg = (UserLeaveGroupMessage)list.SelectMany(l => l.ReceivedMessages).Single();
                    Assert.Equal(userId, msg.UserId);
                    Assert.Equal(group, msg.GroupName);
                }
            }

            await MockConnectionTestAsync(serviceTransportType, testAction, assertAction);
        }

        [InlineData(ServiceTransportType.Persistent)]
        [Theory]
        public async Task UserLeaveAllGroup_Test(ServiceTransportType serviceTransportType)
        {
            var userId = "User";

            Task testAction(ServiceHubContext hubContext) => hubContext.UserGroups.RemoveFromAllGroupsAsync(userId);

            void assertAction(Dictionary<HubServiceEndpoint, List<TestServiceConnection>> createdConnections)
            {
                foreach (var list in createdConnections.Values)
                {
                    var msg = (UserLeaveGroupMessage)list.SelectMany(l => l.ReceivedMessages).Single();
                    Assert.Equal(userId, msg.UserId);
                    Assert.Null(msg.GroupName);
                }
            }

            await MockConnectionTestAsync(serviceTransportType, testAction, assertAction);
        }

        private async Task MockConnectionTestAsync(ServiceTransportType serviceTransportType, Func<ServiceHubContext, Task> testAction, Action<Dictionary<HubServiceEndpoint, List<TestServiceConnection>>> assertAction)
        {
            using (StartLog(out var loggerFactory, LogLevel.Debug))
            {
                var connectionFactory = new TestServiceConnectionFactory();
                var serviceManager = new ServiceManagerBuilder()
                    .WithOptions(o =>
                    {
                        o.ServiceTransportType = serviceTransportType;
                        o.ServiceEndpoints = ServiceEndpoints;
                    })
                    .WithLoggerFactory(loggerFactory)
                    .ConfigureServices(services => services.AddSingleton<IServiceConnectionFactory>(connectionFactory))
                    .BuildServiceManager();
                var hubContext = await serviceManager.CreateHubContextAsync(Hub,default);

                await testAction.Invoke(hubContext);

                var createdConnections = connectionFactory.CreatedConnections.ToDictionary(p => p.Key, p => p.Value.Select(conn => conn as TestServiceConnection).ToList());
                assertAction.Invoke(createdConnections);
            }
        }
    }
}