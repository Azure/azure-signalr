// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Tests;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class NegotiateProcessorFacts
    {
        private const string AccessKey = "fake_key";
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
            var provider = new ServiceCollection().AddSignalRServiceManager()
            .Configure<ServiceManagerOptions>(o =>
            {
                o.ApplicationName = appName;
                o.ServiceEndpoints = endpoints;
                o.ServiceTransportType = ServiceTransportType.Persistent;
            })
            .AddSingleton(router).BuildServiceProvider();
            var negotiateProcessor = provider.GetRequiredService<NegotiateProcessor>();
            for (int i = 0; i < 3; i++)
            {
                var negotiationResponse = await negotiateProcessor.NegotiateAsync(HubName, new NegotiationOptions { UserId = userId, Claims = claims, TokenLifetime = _tokenLifeTime });
                var tokenString = negotiationResponse.AccessToken;
                var token = JwtTokenHelper.JwtHandler.ReadJwtToken(tokenString);

                string expectedToken = JwtTokenHelper.GenerateJwtBearer(ClientEndpointUtils.GetExpectedClientEndpoint(HubName, appName, endpoints[i].Endpoint), ClaimsUtility.BuildJwtClaims(null, userId, () => claims), token.ValidTo, token.ValidFrom, token.ValidFrom, endpoints[i].AccessKey);

                Assert.Equal(ClientEndpointUtils.GetExpectedClientEndpoint(HubName, appName, endpoints[i].Endpoint), negotiationResponse.Url);
                Assert.Equal(expectedToken, tokenString);
            }
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public async Task GetDiagnosticClientNegotiateResponseTest(bool isDiagnosticClient, bool hasClaims)
        {
            var endpoints = FakeEndpointUtils.GetFakeEndpoint(1).ToArray();
            var provider = new ServiceCollection().AddSignalRServiceManager()
            .Configure<ServiceManagerOptions>(o =>
            {
                o.ServiceEndpoints = endpoints;
                o.ServiceTransportType = ServiceTransportType.Persistent;
            }).BuildServiceProvider();
            var userId = "user";
            var negotiateProcessor = provider.GetRequiredService<NegotiateProcessor>();
            var negotiationResponse = await negotiateProcessor.NegotiateAsync(
                HubName,
                new NegotiationOptions
                {
                    UserId = userId,
                    Claims = hasClaims ? new List<Claim> { new Claim("a", "1") } : null,
                    IsDiagnosticClient = isDiagnosticClient,
                    TokenLifetime = _tokenLifeTime
                });
            var tokenString = negotiationResponse.AccessToken;
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(tokenString);

            Assert.True(
                isDiagnosticClient && token.Claims.Any(c => c.Type == Constants.ClaimType.DiagnosticClient && c.Value == "true") ||
                !isDiagnosticClient && !token.Claims.Any(c => c.Type == Constants.ClaimType.DiagnosticClient));
            Assert.True(
                hasClaims && token.Claims.Any(c => c.Type == "a" && c.Value == "1") ||
                !hasClaims && !token.Claims.Any(c => c.Type == "a"));
        }

        [Fact]
        internal async Task GenerateClientEndpointTestWithClientEndpoint()
        {
            var endpoints = new ServiceEndpoint[] { new ServiceEndpoint($"Endpoint=http://localhost;AccessKey={AccessKey};Version=1.0;ClientEndpoint=https://remote") };
            var provider = new ServiceCollection().AddSignalRServiceManager().Configure<ServiceManagerOptions>(o =>
            {
                o.ServiceEndpoints = endpoints;
                o.ServiceTransportType = ServiceTransportType.Persistent;
            }).BuildServiceProvider();
            var negotiateProcessor = provider.GetRequiredService<NegotiateProcessor>();
            var negotiationResponse = (await negotiateProcessor.NegotiateAsync(HubName, null)).Url;
            Assert.Equal("https://remote/client/?hub=signalrbench", negotiationResponse);
        }
    }
}