// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class MultiServiceManagerFacts
    {
        private const int Count = 3;
        private readonly ServiceEndpoint[] _endpoints = Enumerable.Range(0, Count)
    .Select(id => new ServiceEndpoint($"Endpoint=http://endpoint{id};AccessKey=accessKey;Version=1.0;")).ToArray();

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
            var multiServiceManager = new MultiServiceManager(mockManagers, endpoints, mockRouter);

            var actual = await multiServiceManager.IsServiceHealthy(default);

            Assert.Equal(expected, actual);
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
            var multiServiceManager = new MultiServiceManager(managers, _endpoints, mockRouter);

            var multiServiceHubContext = multiServiceManager.CreateHubContextAsync(hubName, loggerFactory, cancellationToken);

            Assert.NotNull(multiServiceHubContext);
            foreach (var mock in mocks)
            {
                mock.Verify(managers => managers.CreateHubContextAsync(hubName, loggerFactory, cancellationToken));
            }
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
            var multiServiceManager = new MultiServiceManager(mockManagers, _endpoints, mockRouter);

            var (endpoint, accessToken) = multiServiceManager.GenerateClientEndpointAndAccessTokenPair(context, hubName, userId, claims, lifeTime);

            Assert.Equal(endpoint_tokenPairs[0].Item1, endpoint);
            Assert.Equal(endpoint_tokenPairs[0].Item2, accessToken);
        }
    }
}