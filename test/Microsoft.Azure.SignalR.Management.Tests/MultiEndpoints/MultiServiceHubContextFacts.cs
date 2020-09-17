// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests.MultiEndpoints
{
    public class MultiServiceHubContextFacts
    {
        private const int Count = 3;
        private readonly ServiceEndpoint[] _endpoints = MultiEndpointUtils.GenerateServiceEndpoints(Count);

        [Fact]
        public void GetClientsPropertyTest()
        {
            var multiServiceHubContext = CreateMultiServiceHubContext();
            var clients = multiServiceHubContext.Clients;

            Assert.NotNull(clients);
        }

        [Fact]
        public void GetUserGroupsPropertyTest()
        {
            var multiServiceHubContext = CreateMultiServiceHubContext();
            var userGroups = multiServiceHubContext.UserGroups;

            Assert.NotNull(userGroups);
        }

        [Fact]
        public void GetGroupsPropertyTest()
        {
            var multiServiceHubContext = CreateMultiServiceHubContext();
            var groups = multiServiceHubContext.Groups;

            Assert.NotNull(groups);
        }

        [Fact]
        public async Task DisposeAsyncNormalTest()
        {
            var contexts = _endpoints.Select(_ =>
            {
                var mock = new Mock<IServiceHubContext>();
                mock.Setup(context => context.DisposeAsync()).Returns(Task.CompletedTask);
                return mock.Object;
            });
            var router = Mock.Of<IEndpointRouter>();
            var table = _endpoints.Zip(contexts).ToDictionary(pair => pair.First, pair => pair.Second);
            var multiServiceHubContext = new MultiServiceHubContext(router, table);

            await multiServiceHubContext.DisposeAsync();
        }

        [Fact]
        public async Task DisposeAsyncThrownTest()
        {
            var contexts = _endpoints.Select(_ =>
            {
                var mock = new Mock<IServiceHubContext>();
                mock.Setup(context => context.DisposeAsync()).ThrowsAsync(new Exception());
                return mock.Object;
            });
            var router = Mock.Of<IEndpointRouter>();
            var table = _endpoints.Zip(contexts).ToDictionary(pair => pair.First, pair => pair.Second);
            var multiServiceHubContext = new MultiServiceHubContext(router, table);

            var t = multiServiceHubContext.DisposeAsync();

            await t.AssertThrowAggregationException(Count);
        }

        private MultiServiceHubContext CreateMultiServiceHubContext()
        {
            var contexts = _endpoints.Select(_ =>
            {
                var mock = new Mock<IServiceHubContext>();
                mock.SetupAllProperties();
                return mock.Object;
            });
            var router = Mock.Of<IEndpointRouter>();
            var table = _endpoints.Zip(contexts).ToDictionary(pair => pair.First, pair => pair.Second);
            return new MultiServiceHubContext(router, table);
        }
    }
}