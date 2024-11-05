// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Management.Tests;

public class InternalServiceHubContextFacts(ITestOutputHelper output) : VerifiableLoggedTest(output)
{
    private const string Hub = nameof(Hub);

    private const string UserId = "User";

    private const string GroupName = "Group";

    private const int Count = 3;

    private static readonly ServiceEndpoint[] ServiceEndpoints = FakeEndpointUtils.GetFakeEndpoint(Count).ToArray();

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
        services.AddSingleton<IReadOnlyCollection<ServiceDescriptor>>([.. services]);
        var serviceManager = services.BuildServiceProvider().GetRequiredService<IServiceManager>();

        var hubContext = (await serviceManager.CreateHubContextAsync(Hub) as ServiceHubContext)
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
            var negotiationResponse = await hubContext
                .WithEndpoints([randomEndpoint])
                .NegotiateAsync();

            Assert.Equal(ClientEndpointUtils.GetExpectedClientEndpoint(Hub, null, randomEndpoint.Endpoint), negotiationResponse.Url);
            var tokenString = negotiationResponse.AccessToken;
            var token = JwtTokenHelper.JwtHandler.ReadJwtToken(tokenString);
            var expectedToken = JwtTokenHelper.GenerateJwtBearer(
                ClientEndpointUtils.GetExpectedClientEndpoint(Hub, null, randomEndpoint.Endpoint),
                ClaimsUtility.BuildJwtClaims(null, null, null),
                token.ValidTo,
                token.ValidFrom,
                token.ValidFrom,
                randomEndpoint.AccessKey);
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

        Task testAction(ServiceHubContext hubContext)
        {
            // no need to wait for ack
            _ = hubContext.UserGroups.AddToGroupAsync(UserId, GroupName, ttl);
            return Task.CompletedTask;
        }
        void assertAction(Dictionary<HubServiceEndpoint, List<TestServiceConnection>> createdConnections)
        {
            foreach (var list in createdConnections.Values)
            {
                var msg = (UserJoinGroupWithAckMessage)list.SelectMany(l => l.ReceivedMessages).Single();
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

        Task testAction(ServiceHubContext hubContext)
        {
            // no need to wait for ack
            _ = hubContext.UserGroups.RemoveFromGroupAsync(userId, group);
            return Task.CompletedTask;
        }
        void assertAction(Dictionary<HubServiceEndpoint, List<TestServiceConnection>> createdConnections)
        {
            foreach (var list in createdConnections.Values)
            {
                var msg = (UserLeaveGroupWithAckMessage)list.SelectMany(l => l.ReceivedMessages).Single();
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

        Task testAction(ServiceHubContext hubContext)
        {
            // no need to wait for ack
            _ = hubContext.UserGroups.RemoveFromAllGroupsAsync(userId);
            return Task.CompletedTask;
        }
        void assertAction(Dictionary<HubServiceEndpoint, List<TestServiceConnection>> createdConnections)
        {
            foreach (var list in createdConnections.Values)
            {
                var msg = (UserLeaveGroupWithAckMessage)list.SelectMany(l => l.ReceivedMessages).Single();
                Assert.Equal(userId, msg.UserId);
                Assert.Null(msg.GroupName);
            }
        }

        await MockConnectionTestAsync(serviceTransportType, testAction, assertAction);
    }

    [Fact]
    public async Task AddNotExistedConnectionToGroup_NoError_Test()
    {
        using var disposable = StartLog(out var loggerFactory, LogLevel.Debug);
        var hubContext = await new ServiceManagerBuilder()
        .WithOptions(o =>
        {
            o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).Single();
            o.ServiceTransportType = ServiceTransportType.Transient;
        })
        .WithLoggerFactory(loggerFactory)
        .ConfigureServices(services => services.AddHttpClient(Constants.HttpClientNames.Resilient).AddHttpMessageHandler(sp =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.NotFound);
                response.Headers.Add(Constants.Headers.MicrosoftErrorCode, "Error.Connection.NotExisted");
                var mock = new Mock<DelegatingHandler>();
                mock.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);
                return mock.Object;
            }))
        .BuildServiceManager()
        .CreateHubContextAsync(Hub, default);
        await hubContext.Groups.AddToGroupAsync(Guid.NewGuid().ToString(), GroupName);
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
            var hubContext = await serviceManager.CreateHubContextAsync(Hub, default);

            await testAction.Invoke(hubContext);

            var createdConnections = connectionFactory.CreatedConnections.ToDictionary(p => p.Key, p => p.Value.Select(conn => conn as TestServiceConnection).ToList());
            assertAction.Invoke(createdConnections);
        }
    }
}