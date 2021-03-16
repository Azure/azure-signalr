// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class MultiEndpointServiceHubContextFacts : VerifiableLoggedTest
    {
        private const string Hub = nameof(Hub);
        private const string UserId = "User";
        private const string GroupName = "Group";
        private static readonly ServiceEndpoint[] ServiceEndpoints = FakeEndpointUtils.GetFakeEndpoint(3).ToArray();

        public MultiEndpointServiceHubContextFacts(ITestOutputHelper output) : base(output)
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
        public async Task UserJoinGroup_Test(ServiceTransportType serviceTransportType)
        {
            Task testAction(ServiceHubContext hubContext) => hubContext.UserGroups.AddToGroupAsync(UserId, GroupName);

            void assertAction(ConcurrentDictionary<HubServiceEndpoint, List<MessageVerifiableConnection>> createdConnections)
            {
                foreach (var list in createdConnections.Values)
                {
                    var msg = (UserJoinGroupMessage)list.SelectMany(l => l.ReceivedMessages).Single();
                    Assert.Equal(UserId, msg.UserId);
                    Assert.Equal(GroupName, msg.GroupName);
                }
            }

            await RunCoreAsync(serviceTransportType, testAction, assertAction);
        }

        [InlineData(ServiceTransportType.Persistent)]
        [Theory]
        public async Task UserJoinGroupWithTTL_Test(ServiceTransportType serviceTransportType)
        {
            var ttl = TimeSpan.FromSeconds(1);

            Task testAction(ServiceHubContext hubContext) => hubContext.UserGroups.AddToGroupAsync(UserId, GroupName, ttl);

            void assertAction(ConcurrentDictionary<HubServiceEndpoint, List<MessageVerifiableConnection>> createdConnections)
            {
                foreach (var list in createdConnections.Values)
                {
                    var msg = (UserJoinGroupMessage)list.SelectMany(l => l.ReceivedMessages).Single();
                    Assert.Equal(UserId, msg.UserId);
                    Assert.Equal(GroupName, msg.GroupName);
                    Assert.Equal((int)ttl.TotalSeconds, msg.Ttl);
                }
            }

            await RunCoreAsync(serviceTransportType, testAction, assertAction);
        }

        [InlineData(ServiceTransportType.Persistent)]
        [Theory]
        public async Task UserLeaveGroup_Test(ServiceTransportType serviceTransportType)
        {
            var userId = "User";
            var group = "Group";

            Task testAction(ServiceHubContext hubContext) => hubContext.UserGroups.RemoveFromGroupAsync(userId, group);

            void assertAction(ConcurrentDictionary<HubServiceEndpoint, List<MessageVerifiableConnection>> createdConnections)
            {
                foreach (var list in createdConnections.Values)
                {
                    var msg = (UserLeaveGroupMessage)list.SelectMany(l => l.ReceivedMessages).Single();
                    Assert.Equal(userId, msg.UserId);
                    Assert.Equal(group, msg.GroupName);
                }
            }

            await RunCoreAsync(serviceTransportType, testAction, assertAction);
        }

        [InlineData(ServiceTransportType.Persistent)]
        [Theory]
        public async Task UserLeaveAllGroup_Test(ServiceTransportType serviceTransportType)
        {
            var userId = "User";

            Task testAction(ServiceHubContext hubContext) => hubContext.UserGroups.RemoveFromAllGroupsAsync(userId);

            void assertAction(ConcurrentDictionary<HubServiceEndpoint, List<MessageVerifiableConnection>> createdConnections)
            {
                foreach (var list in createdConnections.Values)
                {
                    var msg = (UserLeaveGroupMessage)list.SelectMany(l => l.ReceivedMessages).Single();
                    Assert.Equal(userId, msg.UserId);
                    Assert.Null(msg.GroupName);
                }
            }

            await RunCoreAsync(serviceTransportType, testAction, assertAction);
        }

        private async Task RunCoreAsync(ServiceTransportType serviceTransportType, Func<ServiceHubContext, Task> testAction, Action<ConcurrentDictionary<HubServiceEndpoint, List<MessageVerifiableConnection>>> assertAction)
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {
                var connectionFactory = new TestServiceConnectionFactory();
                var hubContext = await new ServiceHubContextBuilder()
                                .WithOptions(o =>
                                {
                                    o.ServiceTransportType = serviceTransportType;
                                    o.ServiceEndpoints = ServiceEndpoints;
                                })
                                .WithLoggerFactory(loggerFactory)
                                .ConfigureServices(services => services.AddSingleton<IServiceConnectionFactory>(connectionFactory))
                                .CreateAsync(Hub, default);

                await testAction.Invoke(hubContext);

                var createdConnections = connectionFactory.CreatedConnections;
                assertAction.Invoke(createdConnections);
            }
        }
    }
}