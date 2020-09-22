// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests.MultiEndpoints
{
    public class MultiServiceManagerFacts
    {
        private const int Count = 3;
        private readonly ServiceEndpoint[] _endpoints = MultiEndpointUtils.GenerateServiceEndpoints(Count);

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, true, true)]
        [InlineData(false, false)]
        [InlineData(false, true, false)]
        public async Task IsServiceHealthyNormalTest(bool expected, params bool[] serviceHealthStatus)
        {
            var mockRouter = Mock.Of<IEndpointRouter>();
            IEnumerable<IServiceManager> mockManagers = serviceHealthStatus
                .Select(status =>
                {
                    var mock = new Mock<IServiceManager>();
                    mock.Setup(manager => manager.IsServiceHealthy(default))
                        .Returns(Task.FromResult(status));
                    return mock.Object;
                });
            IEnumerable<ServiceEndpoint> endpoints = Enumerable
                .Range(0, serviceHealthStatus.Length)
                .Select(id => new ServiceEndpoint($"Endpoint=http://endpoint{id};AccessKey=accessKey;Version=1.0;"));
            using var multiServiceManager = new MultiServiceManager(mockManagers, endpoints, mockRouter);

            var actual = await multiServiceManager.IsServiceHealthy(default);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task IsServiceHealthyThrownTest()
        {
            var mockRouter = Mock.Of<IEndpointRouter>();
            IEnumerable<IServiceManager> mockManagers = _endpoints
                .Select(status =>
                {
                    var mock = new Mock<IServiceManager>();
                    mock.Setup(manager => manager.IsServiceHealthy(default))
                        .ThrowsAsync(new Exception());
                    return mock.Object;
                });
            using var multiServiceManager = new MultiServiceManager(mockManagers, _endpoints, mockRouter);

            Task t = multiServiceManager.IsServiceHealthy(default);

            await t.AssertThrowAggregationException(Count);
        }

        [Fact]
        public void CreateHubContextAsyncNormalTest()
        {
            var hubName = "hub_1";
            var loggerFactory = default(ILoggerFactory);
            var cancellationToken = default(CancellationToken);
            var mockRouter = Mock.Of<IEndpointRouter>();
            var mocks = _endpoints
            .Select(endpoint =>
            {
                var managerMock = new Mock<IServiceManager>();
                var serviceHubContextMock = new Mock<IServiceHubContext>();
                serviceHubContextMock.SetupAllProperties();
                managerMock.Setup(manager => manager.CreateHubContextAsync(hubName, loggerFactory, cancellationToken)).ReturnsAsync(serviceHubContextMock.Object);
                return managerMock;
            }).ToArray();
            var managers = from mock in mocks select mock.Object;
            using var multiServiceManager = new MultiServiceManager(managers, _endpoints, mockRouter);

            var multiServiceHubContext = multiServiceManager.CreateHubContextAsync(hubName, loggerFactory, cancellationToken);

            Assert.NotNull(multiServiceHubContext);
            foreach (var mock in mocks)
            {
                mock.Verify(managers => managers.CreateHubContextAsync(hubName, loggerFactory, cancellationToken));
            }
        }

        [Fact]
        public async Task CreateHubContextAsyncThrownTest()
        {
            var hubName = "hub_1";
            var loggerFactory = default(ILoggerFactory);
            var cancellationToken = default(CancellationToken);
            var mockRouter = Mock.Of<IEndpointRouter>();
            var mocks = _endpoints
            .Select(endpoint =>
            {
                var managerMock = new Mock<IServiceManager>();
                var serviceHubContextMock = new Mock<IServiceHubContext>();
                serviceHubContextMock.SetupAllProperties();
                managerMock.Setup(manager => manager.CreateHubContextAsync(hubName, loggerFactory, cancellationToken)).ThrowsAsync(new Exception());
                return managerMock;
            }).ToArray();
            var managers = from mock in mocks select mock.Object;
            using var multiServiceManager = new MultiServiceManager(managers, _endpoints, mockRouter);

            var task = multiServiceManager.CreateHubContextAsync(hubName, loggerFactory, cancellationToken);

            await task.AssertThrowAggregationException(Count);
        }

        [Fact]
        public void GenerateClientEndpointAndAccessTokenPairTest()
        {
            var hubName = "hubName";
            var context = default(HttpContext);
            var userId = "user_1";
            var claims = default(IList<Claim>);
            var lifeTime = default(TimeSpan);
            var mockRouter = Mock.Of<IEndpointRouter>(router => router.GetNegotiateEndpoint(context, _endpoints) == _endpoints[0]);
            var endpoint_tokenPairs = Enumerable.Range(0, Count).Select(id => ($"client_endpoint_{id}", $"token_{id}")).ToArray();
            IEnumerable<IServiceManager> mockManagers = endpoint_tokenPairs
                .Select(pair =>
                {
                    var mock = new Mock<IServiceManager>();
                    mock.Setup(manager => manager.GenerateClientAccessToken(hubName, userId, claims, lifeTime)).Returns(pair.Item2);
                    mock.Setup(manager => manager.GetClientEndpoint(hubName)).Returns(pair.Item1);
                    return mock.Object;
                });
            using var multiServiceManager = new MultiServiceManager(mockManagers, _endpoints, mockRouter);

            var (endpoint, accessToken) = multiServiceManager.GenerateClientEndpointAndAccessTokenPair(context, hubName, userId, claims, lifeTime);

            Assert.Equal(endpoint_tokenPairs[0].Item1, endpoint);
            Assert.Equal(endpoint_tokenPairs[0].Item2, accessToken);
        }

        [Fact]
        public void NoRoutingAfterHealthCheckReturnFalseTest()
        {
            var router = new EndpointRouterDecorator();
            IEnumerable<IServiceManager> mockManagers = Enumerable.Range(0, Count)
            .Select(_ =>
            {
                var mock = new Mock<IServiceManager>();
                mock.Setup(manager => manager.IsServiceHealthy(It.IsAny<CancellationToken>())).ReturnsAsync(false);
                return mock.Object;
            });

            using var multiServiceManager = new MultiServiceManager(mockManagers, _endpoints, router);
            Assert.Throws<AzureSignalRNotConnectedException>(() => multiServiceManager.GenerateClientEndpointAndAccessTokenPair(default, "hub_1", "user_1", default, default));
        }
    }
}