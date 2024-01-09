// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class ServiceEndpointProviderFacts
    {
        private const string Endpoint = "https://myendpoint";
        private const string AccessKey = "fake_key";
        private static readonly string HubName = nameof(TestHub).ToLower();
        private static readonly string AppName = "testapp";

        private static readonly string ConnectionStringWithoutVersion =
            $"Endpoint={Endpoint};AccessKey={AccessKey};";

        private static readonly string ConnectionStringWithPreviewVersion =
            $"Endpoint={Endpoint};AccessKey={AccessKey};Version=1.0-preview";

        private static readonly string ConnectionStringWithV1Version = $"Endpoint={Endpoint};AccessKey={AccessKey};Version=1.0";

        private static readonly ServiceOptions _optionsWithoutAppName = Options.Create(new ServiceOptions()).Value;
        private static readonly ServiceOptions _optionsWithAppName = Options.Create(new ServiceOptions { ApplicationName = AppName }).Value;

        private static readonly ServiceEndpointProvider[] EndpointProviderArray =
        {
            new ServiceEndpointProvider(new ServiceEndpoint(ConnectionStringWithoutVersion), _optionsWithoutAppName),
            new ServiceEndpointProvider(new ServiceEndpoint(ConnectionStringWithPreviewVersion), _optionsWithoutAppName),
            new ServiceEndpointProvider(new ServiceEndpoint(ConnectionStringWithV1Version), _optionsWithoutAppName)
        };

        private static readonly ServiceEndpointProvider[] EndpointProviderArrayWithPrefix =
        {
            new ServiceEndpointProvider(new ServiceEndpoint(ConnectionStringWithoutVersion), _optionsWithAppName),
            new ServiceEndpointProvider(new ServiceEndpoint(ConnectionStringWithPreviewVersion), _optionsWithAppName),
            new ServiceEndpointProvider(new ServiceEndpoint(ConnectionStringWithV1Version), _optionsWithAppName)
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
            EndpointProviderArray.Select(provider => new object[] { provider });

        public static IEnumerable<object[]> PathAndQueries =>
            PathAndQueryArray.Select(t => new object[] { t.path, t.queryString, t.expectedQuery });

        public static IEnumerable<object[]> DefaultEndpointProvidersWithPath =>
            from provider in EndpointProviderArray
            from t in PathAndQueryArray
            select new object[] { provider, t.path, t.queryString, t.expectedQuery };

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
        internal void GetServerEndpointWithAppName(IServiceEndpointProvider provider)
        {
            var expected = $"{Endpoint}/server/?hub={AppName}_{HubName}";
            var actual = provider.GetServerEndpoint(nameof(TestHub));
            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(DefaultEndpointProvidersWithPathPlusPrefix))]
        internal void GetClientEndpointWithAppName(IServiceEndpointProvider provider, string path, string queryString, string expectedQueryString)
        {
            var expected = $"{Endpoint}/client/?hub={AppName}_{HubName}{expectedQueryString}";
            var actual = provider.GetClientEndpoint(HubName, path, queryString);
            Assert.Equal(expected, actual);
        }

        [Fact(Skip = "Access token does not need to be unique")]
        internal async Task GenerateMultipleAccessTokenShouldBeUnique()
        {
            var count = 1000;
            var sep = new ServiceEndpointProvider(new ServiceEndpoint(ConnectionStringWithPreviewVersion), _optionsWithoutAppName);
            var userId = Guid.NewGuid().ToString();
            var tokens = new List<string>();
            for (var i = 0; i < count; i++)
            {
                tokens.Add(await sep.GenerateClientAccessTokenAsync(nameof(TestHub)));
                tokens.Add(await sep.GetServerAccessTokenProvider(nameof(TestHub), userId).ProvideAsync());
            }

            var distinct = tokens.Distinct();
            Assert.Equal(tokens.Count, distinct.Count());
        }

        [Theory]
        [MemberData(nameof(DefaultEndpointProviders))]
        internal async Task GenerateServerAccessToken(IServiceEndpointProvider provider)
        {
            const string userId = "UserA";
            var tokenString = await provider.GetServerAccessTokenProvider(nameof(TestHub), userId).ProvideAsync();
            var token = JwtTokenHelper.JwtHandler.ReadJwtToken(tokenString);

            var expectedTokenString = JwtTokenHelper.GenerateJwtBearer($"{Endpoint}/server/?hub={HubName}",
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId)
                },
                token.ValidTo,
                token.ValidFrom,
                token.ValidFrom,
                AccessKey
            );

            Assert.Equal(expectedTokenString, tokenString);
        }

        [Theory]
        [MemberData(nameof(DefaultEndpointProvidersPlusPrefix))]
        internal async Task GenerateServerAccessTokenWithPrefix(IServiceEndpointProvider provider)
        {
            const string userId = "UserA";
            var tokenString = await provider.GetServerAccessTokenProvider(nameof(TestHub), userId).ProvideAsync();
            var token = JwtTokenHelper.JwtHandler.ReadJwtToken(tokenString);

            var expectedTokenString = JwtTokenHelper.GenerateJwtBearer($"{Endpoint}/server/?hub={AppName}_{HubName}",
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId)
                },
                token.ValidTo,
                token.ValidFrom,
                token.ValidFrom,
                AccessKey
            );

            Assert.Equal(expectedTokenString, tokenString);
        }

        [Theory]
        [MemberData(nameof(DefaultEndpointProviders))]
        internal async Task GenerateClientAccessToken(IServiceEndpointProvider provider)
        {
            var requestId = Guid.NewGuid().ToString();
            var tokenString = await provider.GenerateClientAccessTokenAsync(HubName);
            var token = JwtTokenHelper.JwtHandler.ReadJwtToken(tokenString);

            var expectedTokenString = JwtTokenHelper.GenerateJwtBearer($"{Endpoint}/client/?hub={HubName}",
                null,
                token.ValidTo,
                token.ValidFrom,
                token.ValidFrom,
                AccessKey
            );

            Assert.Equal(expectedTokenString, tokenString);
        }

        [Theory]
        [MemberData(nameof(DefaultEndpointProvidersPlusPrefix))]
        internal async Task GenerateClientAccessTokenWithPrefix(IServiceEndpointProvider provider)
        {
            var tokenString = await provider.GenerateClientAccessTokenAsync(HubName);
            var token = JwtTokenHelper.JwtHandler.ReadJwtToken(tokenString);

            var expectedTokenString = JwtTokenHelper.GenerateJwtBearer($"{Endpoint}/client/?hub={AppName}_{HubName}",
                null,
                token.ValidTo,
                token.ValidFrom,
                token.ValidFrom,
                AccessKey
            );

            Assert.Equal(expectedTokenString, tokenString);
        }

        [Theory]
        [InlineData(AccessTokenAlgorithm.HS256)]
        [InlineData(AccessTokenAlgorithm.HS512)]
        public async Task GenerateServerAccessTokenWithSpecifedAlgorithm(AccessTokenAlgorithm algorithm)
        {
            var provider = new ServiceEndpointProvider(new ServiceEndpoint(ConnectionStringWithV1Version), new ServiceOptions() { AccessTokenAlgorithm = algorithm });
            var serverToken = await provider.GetServerAccessTokenProvider("hub1", "user1").ProvideAsync();

            var token = JwtTokenHelper.JwtHandler.ReadJwtToken(serverToken);

            Assert.Equal(algorithm.ToString(), token.SignatureAlgorithm);
        }

        [Theory]
        [InlineData(AccessTokenAlgorithm.HS256)]
        [InlineData(AccessTokenAlgorithm.HS512)]
        public async Task GenerateClientAccessTokenWithSpecifedAlgorithm(AccessTokenAlgorithm algorithm)
        {
            var provider = new ServiceEndpointProvider(new ServiceEndpoint(ConnectionStringWithV1Version), new ServiceOptions() { AccessTokenAlgorithm = algorithm });
            var generatedToken = await provider.GenerateClientAccessTokenAsync("hub1");

            var token = JwtTokenHelper.JwtHandler.ReadJwtToken(generatedToken);

            Assert.Equal(algorithm.ToString(), token.SignatureAlgorithm);
        }
    }
}
