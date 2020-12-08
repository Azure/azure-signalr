// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Tests;
using Microsoft.Azure.SignalR.Tests.Common;
using Moq;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class ServiceContextFacts
    {
        private const string AccessKey = "nOu3jXsHnsO5urMumc87M9skQbUWuQ+PE5IvSUEic8w=";
        private const string HubName = "signalrBench";
        private const string UserId = "UserA";
        private static readonly TimeSpan _tokenLifeTime = TimeSpan.FromSeconds(99);
        private static readonly Claim[] _defaultClaims = new Claim[] { new Claim("type1", "val1") };
        private static readonly string[] _appNames = new string[] { "appName", "", null };
        private static readonly string[] _userIds = new string[] { UserId, null };
        private static readonly IEnumerable<Claim[]> _claimLists = new Claim[][] { _defaultClaims, null };

        public static IEnumerable<object[]> TestGenerateAccessTokenData => from userId in _userIds
                                                                           from claims in _claimLists
                                                                           from appName in _appNames
                                                                           select new object[] { userId, claims, appName };

        [Theory]
        [MemberData(nameof(TestGenerateAccessTokenData))]
        public async Task GenerateClientEndpoint(string userId, Claim[] claims, string appName)
        {
            var endpoints = FakeEndpointUtils.GetFakeEndpoint(3).ToArray();
            var routerMock = new Mock<IEndpointRouter>();
            routerMock.SetupSequence(router => router.GetNegotiateEndpoint(null, endpoints))
                .Returns(endpoints[0])
                .Returns(endpoints[1])
                .Returns(endpoints[2]);
            var router = routerMock.Object;
            var manager = new ServiceContextBuilder()
            .WithOptions(o =>
            {
                o.ApplicationName = appName;
                o.ServiceEndpoints = endpoints;
            })
            .WithRouter(router).Build();
            for (int i = 0; i < 3; i++)
            {
                var accessInfo = await manager.GetClientEndpointAsync(HubName, null, userId, claims, _tokenLifeTime);
                var tokenString = accessInfo.AccessToken;
                var token = JwtTokenHelper.JwtHandler.ReadJwtToken(tokenString);

                string expectedToken = JwtTokenHelper.GenerateJwtBearer(ClientEndpointUtils.GetExpectedClientEndpoint(HubName, appName, endpoints[i].Endpoint), ClaimsUtility.BuildJwtClaims(null, userId, () => claims),token.ValidTo,token.ValidFrom,token.ValidFrom,endpoints[i].AccessKey);

                Assert.Equal(ClientEndpointUtils.GetExpectedClientEndpoint(HubName, appName, endpoints[i].Endpoint), accessInfo.Url);
                Assert.Equal(expectedToken, tokenString);
            }
        }

        [Fact]
        internal async Task GenerateClientEndpointTestWithClientEndpoint()
        {
            var endpoints = new ServiceEndpoint[] { new ServiceEndpoint($"Endpoint=http://localhost;AccessKey={AccessKey};Version=1.0;ClientEndpoint=https://remote") };
            //if no mock router, then error throws because all endpoints are offline.
            var routerMock = new Mock<IEndpointRouter>();
            routerMock.Setup(router => router.GetNegotiateEndpoint(null, endpoints)).Returns(endpoints.Single());
            var manager = new ServiceContextBuilder().WithOptions(o =>
            {
                o.ServiceEndpoints = endpoints;
            }).WithRouter(routerMock.Object).Build();
            var clientEndpoint = (await manager.GetClientEndpointAsync(HubName)).Url;
            Assert.Equal("https://remote/client/?hub=signalrbench", clientEndpoint);
        }
    }
}