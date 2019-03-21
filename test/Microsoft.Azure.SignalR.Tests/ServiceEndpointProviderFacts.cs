﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class ServiceEndpointProviderFacts
    {
        private const string Endpoint = "https://myendpoint";
        private const string AccessKey = "nOu3jXsHnsO5urMumc87M9skQbUWuQ+PE5IvSUEic8w=";
        private static readonly string HubName = nameof(TestHub).ToLower();
        private static readonly string HubPrefix = "testprefix";

        private static readonly string ConnectionStringWithoutVersion =
            $"Endpoint={Endpoint};AccessKey={AccessKey};";

        private static readonly string ConnectionStringWithPreviewVersion =
            $"Endpoint={Endpoint};AccessKey={AccessKey};Version=1.0-preview";

        private static readonly string ConnectionStringWithV1Version = $"Endpoint={Endpoint};AccessKey={AccessKey};Version=1.0";

        private static readonly ServiceEndpointProvider[] EndpointProviderArray =
        {
            new ServiceEndpointProvider(new ServiceEndpoint(ConnectionStringWithoutVersion)),
            new ServiceEndpointProvider(new ServiceEndpoint(ConnectionStringWithPreviewVersion)),
            new ServiceEndpointProvider(new ServiceEndpoint(ConnectionStringWithV1Version))
        };

        private static readonly ServiceEndpointProvider[] EndpointProviderArrayWithPrefix =
{
            new ServiceEndpointProvider(new ServiceEndpoint(ConnectionStringWithoutVersion, hubPrefix: HubPrefix)),
            new ServiceEndpointProvider(new ServiceEndpoint(ConnectionStringWithPreviewVersion, hubPrefix: HubPrefix)),
            new ServiceEndpointProvider(new ServiceEndpoint(ConnectionStringWithV1Version, hubPrefix: HubPrefix))
        };

        private static readonly (string path, string queryString, string expectedQuery)[] PathAndQueryArray =
        {
            ("", "", ""),
            (null, "", ""),
            ("/user/path", "", $"&{Constants.QueryParameter.OriginalPath}=%2Fuser%2Fpath"),
            ("", "customKey=customValue", "&customKey=customValue"),
            ("/user/path", "customKey=customValue", $"&{Constants.QueryParameter.OriginalPath}=%2Fuser%2Fpath&customKey=customValue")
        };

        public static IEnumerable<object[]> DefaultEndpointProviders =>
            EndpointProviderArray.Select(provider => new object[] {provider});

        public static IEnumerable<object[]> PathAndQueries =>
            PathAndQueryArray.Select(t => new object[] {t.path, t.queryString, t.expectedQuery});

        public static IEnumerable<object[]> DefaultEndpointProvidersWithPath =>
            from provider in EndpointProviderArray
            from t in PathAndQueryArray
            select new object[] { provider, t.path, t.queryString, t.expectedQuery} ;

        public static IEnumerable<object[]> DefaultEndpointProvidersWithPathPlusPrefix =>
            from provider in EndpointProviderArrayWithPrefix
            from t in PathAndQueryArray
            select new object[] { provider, t.path, t.queryString, t.expectedQuery };

        public static IEnumerable<object[]> DefaultEndpointProvidersPlusPrefix =>
            EndpointProviderArrayWithPrefix.Select(provider => new object[] { provider });

        [Theory]
        [MemberData(nameof(DefaultEndpointProviders))]
        internal void GetServerEndpoint(IServiceEndpointProvider provider)
        {
            var expected = $"{Endpoint}/server/?hub={HubName}";
            var actual = provider.GetServerEndpoint(nameof(TestHub));
            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(DefaultEndpointProvidersWithPath))]
        internal void GetClientEndpoint(IServiceEndpointProvider provider, string path, string queryString, string expectedQueryString)
        {
            var expected = $"{Endpoint}/client/?hub={HubName}{expectedQueryString}";
            var actual = provider.GetClientEndpoint(HubName, path, queryString);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(DefaultEndpointProvidersPlusPrefix))]
        internal void GetServerEndpointWithHubPrefix(IServiceEndpointProvider provider)
        {
            var expected = $"{Endpoint}/server/?hub={HubPrefix}_{HubName}";
            var actual = provider.GetServerEndpoint(nameof(TestHub));
            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(DefaultEndpointProvidersWithPathPlusPrefix))]
        internal void GetClientEndpointWithHubPrefix(IServiceEndpointProvider provider, string path, string queryString, string expectedQueryString)
        {
            var expected = $"{Endpoint}/client/?hub={HubPrefix}_{HubName}{expectedQueryString}";
            var actual = provider.GetClientEndpoint(HubName, path, queryString);
            Assert.Equal(expected, actual);
        }

        [Fact]
        internal void GenerateMutlipleAccessTokenShouldBeUnique()
        {
            var count = 1000;
            var sep = new ServiceEndpointProvider(new ServiceEndpoint(ConnectionStringWithPreviewVersion));
            var userId = Guid.NewGuid().ToString();
            var tokens = new List<string>();
            for (int i = 0; i < count; i++)
            {
                tokens.Add(sep.GenerateClientAccessToken(nameof(TestHub)));
                tokens.Add(sep.GenerateServerAccessToken(nameof(TestHub), userId));
            }

            var distinct = tokens.Distinct();
            Assert.Equal(tokens.Count, distinct.Count());
        }

        [Theory]
        [MemberData(nameof(DefaultEndpointProviders))]
        internal void GenerateServerAccessToken(IServiceEndpointProvider provider)
        {
            const string userId = "UserA";
            var tokenString = provider.GenerateServerAccessToken(nameof(TestHub), userId, requestId: string.Empty);
            var token = JwtTokenHelper.JwtHandler.ReadJwtToken(tokenString);

            var expectedTokenString = JwtTokenHelper.GenerateJwtBearer($"{Endpoint}/server/?hub={HubName}",
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId)
                },
                token.ValidTo,
                token.ValidFrom,
                token.ValidFrom,
                AccessKey,
                string.Empty);

            Assert.Equal(expectedTokenString, tokenString);
        }

        [Theory]
        [MemberData(nameof(DefaultEndpointProvidersPlusPrefix))]
        internal void GenerateServerAccessTokenWithPrefix(IServiceEndpointProvider provider)
        {
            const string userId = "UserA";
            var tokenString = provider.GenerateServerAccessToken(nameof(TestHub), userId, requestId: string.Empty);
            var token = JwtTokenHelper.JwtHandler.ReadJwtToken(tokenString);

            var expectedTokenString = JwtTokenHelper.GenerateJwtBearer($"{Endpoint}/server/?hub={HubPrefix}_{HubName}",
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId)
                },
                token.ValidTo,
                token.ValidFrom,
                token.ValidFrom,
                AccessKey,
                string.Empty);

            Assert.Equal(expectedTokenString, tokenString);
        }

        [Theory]
        [MemberData(nameof(DefaultEndpointProviders))]
        internal void GenerateClientAccessToken(IServiceEndpointProvider provider)
        {
            var requestId = Guid.NewGuid().ToString();
            var tokenString = provider.GenerateClientAccessToken(HubName, requestId: requestId);
            var token = JwtTokenHelper.JwtHandler.ReadJwtToken(tokenString);

            var expectedTokenString = JwtTokenHelper.GenerateJwtBearer($"{Endpoint}/client/?hub={HubName}",
                null,
                token.ValidTo,
                token.ValidFrom,
                token.ValidFrom,
                AccessKey,
                requestId);

            Assert.Equal(expectedTokenString, tokenString);
        }

        [Theory]
        [MemberData(nameof(DefaultEndpointProvidersPlusPrefix))]
        internal void GenerateClientAccessTokenWithPrefix(IServiceEndpointProvider provider)
        {
            var requestId = Guid.NewGuid().ToString();
            var tokenString = provider.GenerateClientAccessToken(HubName, requestId: requestId);
            var token = JwtTokenHelper.JwtHandler.ReadJwtToken(tokenString);

            var expectedTokenString = JwtTokenHelper.GenerateJwtBearer($"{Endpoint}/client/?hub={HubPrefix}_{HubName}",
                null,
                token.ValidTo,
                token.ValidFrom,
                token.ValidFrom,
                AccessKey,
                requestId);

            Assert.Equal(expectedTokenString, tokenString);
        }
    }
}
