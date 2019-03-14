// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class NegotiateHandlerFacts
    {
        private const string CustomClaimType = "custom.claim";
        private const string CustomUserId = "customUserId";
        private const string DefaultUserId = "nameId";
        private const string DefaultConnectionString = "Endpoint=https://localhost;AccessKey=nOu3jXsHnsO5urMumc87M9skQbUWuQ+PE5IvSUEic8w=;";
        private const string ConnectionString2 = "Endpoint=http://localhost2;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;";
        private const string ConnectionString3 = "Endpoint=http://localhost3;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;";
        private const string ConnectionString4 = "Endpoint=http://localhost4;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;";
        private const string UserPath = "/user/path";

        private static readonly JwtSecurityTokenHandler JwtSecurityTokenHandler = new JwtSecurityTokenHandler();

        [Theory]
        [InlineData(typeof(CustomUserIdProvider), CustomUserId)]
        [InlineData(typeof(NullUserIdProvider), null)]
        [InlineData(typeof(DefaultUserIdProvider), DefaultUserId)]
        public void GenerateNegotiateResponseWithUserId(Type type, string expectedUserId)
        {
            var config = new ConfigurationBuilder().Build();
            var serviceProvider = new ServiceCollection().AddSignalR()
                .AddAzureSignalR(
                o =>
                {
                    o.ConnectionString = DefaultConnectionString;
                    o.AccessTokenLifetime = TimeSpan.FromDays(1);
                })
                .Services
                .AddLogging()
                .AddSingleton<IConfiguration>(config)
                .AddSingleton(typeof(IUserIdProvider), type)
                .BuildServiceProvider();

            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(CustomClaimType, CustomUserId),
                    new Claim(ClaimTypes.NameIdentifier, DefaultUserId),
                    new Claim("custom", "custom"),
                }))
            };

            var handler = serviceProvider.GetRequiredService<NegotiateHandler>();
            var negotiateResponse = handler.Process(httpContext, "hub");

            Assert.NotNull(negotiateResponse);
            Assert.NotNull(negotiateResponse.Url);
            Assert.NotNull(negotiateResponse.AccessToken);
            Assert.Null(negotiateResponse.ConnectionId);
            Assert.Empty(negotiateResponse.AvailableTransports);

            var token = JwtSecurityTokenHandler.ReadJwtToken(negotiateResponse.AccessToken);
            Assert.Equal(expectedUserId, token.Claims.FirstOrDefault(x => x.Type == Constants.ClaimType.UserId)?.Value);
            Assert.Equal("custom", token.Claims.FirstOrDefault(x => x.Type == "custom")?.Value);
            Assert.Equal(TimeSpan.FromDays(1), token.ValidTo - token.ValidFrom);
        }

        [Theory]
        [InlineData("/user/path/negotiate", "", "asrs.op=%2Fuser%2Fpath")]
        [InlineData("/user/path/negotiate/", "", "asrs.op=%2Fuser%2Fpath")]
        [InlineData("", "?customKey=customeValue", "customKey=customeValue")]
        [InlineData("/user/path/negotiate", "?customKey=customeValue", "asrs.op=%2Fuser%2Fpath&customKey=customeValue")]
        public void GenerateNegotiateResponseWithPathAndQuery(string path, string queryString, string expectedQueryString)
        {
            var config = new ConfigurationBuilder().Build();
            var serviceProvider = new ServiceCollection().AddSignalR()
                .AddAzureSignalR(o => o.ConnectionString = DefaultConnectionString)
                .Services
                .AddLogging()
                .AddSingleton<IConfiguration>(config)
                .BuildServiceProvider();

            var requestFeature = new HttpRequestFeature
            {
                Path = path,
                QueryString = queryString
            };
            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(requestFeature);
            var httpContext = new DefaultHttpContext(features);

            var handler = serviceProvider.GetRequiredService<NegotiateHandler>();
            var negotiateResponse = handler.Process(httpContext, "chat");

            Assert.NotNull(negotiateResponse);
            Assert.EndsWith($"?hub=chat&{expectedQueryString}", negotiateResponse.Url);
        }

        [Theory]
        [InlineData(typeof(ConnectionIdUserIdProvider), ServiceHubConnectionContext.ConnectionIdUnavailableError)]
        [InlineData(typeof(ConnectionAbortedTokenUserIdProvider), ServiceHubConnectionContext.ConnectionAbortedUnavailableError)]
        [InlineData(typeof(ItemsUserIdProvider), ServiceHubConnectionContext.ItemsUnavailableError)]
        [InlineData(typeof(ProtocolUserIdProvider), ServiceHubConnectionContext.ProtocolUnavailableError)]
        public void CustomUserIdProviderAccessUnavailablePropertyThrowsException(Type type, string errorMessage)
        {
            var config = new ConfigurationBuilder().Build();
            var serviceProvider = new ServiceCollection().AddSignalR()
                .AddAzureSignalR(o => o.ConnectionString = DefaultConnectionString)
                .Services
                .AddLogging()
                .AddSingleton<IConfiguration>(config)
                .AddSingleton(typeof(IUserIdProvider), type)
                .BuildServiceProvider();

            var handler = serviceProvider.GetRequiredService<NegotiateHandler>();
            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal()
            };

            var exception = Assert.Throws<InvalidOperationException>(() => handler.Process(httpContext, "hub"));
            Assert.Equal(errorMessage, exception.Message);
        }

        [Fact]
        public void TestNegotiateHandlerWithMultipleEndpointsAndCustomRouter()
        {
            var config = new ConfigurationBuilder().Build();
            var router = new TestCustomRouter(ConnectionString3);
            var serviceProvider = new ServiceCollection().AddSignalR()
                .AddAzureSignalR(
                o => o.Endpoints = new ServiceEndpoint[]
                {
                    new ServiceEndpoint(ConnectionString2),
                    new ServiceEndpoint(ConnectionString3, name: "chosen"),
                    new ServiceEndpoint(ConnectionString4),
                })
                .Services
                .AddLogging()
                .AddSingleton<IEndpointRouter>(router)
                .AddSingleton<IConfiguration>(config)
                .BuildServiceProvider();

            var requestFeature = new HttpRequestFeature
            {
                Path = "/user/path/negotiate/",
                QueryString = "?endpoint=chosen"
            };

            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(requestFeature);
            var httpContext = new DefaultHttpContext(features);

            var handler = serviceProvider.GetRequiredService<NegotiateHandler>();
            var negotiateResponse = handler.Process(httpContext, "chat");

            Assert.NotNull(negotiateResponse);
            Assert.Equal($"http://localhost3/client/?hub=chat&asrs.op=%2Fuser%2Fpath&endpoint=chosen", negotiateResponse.Url);
        }

        private class TestCustomRouter : EndpointRouterDecorator
        {
            private readonly string _negotiateEndpoint;

            public TestCustomRouter(string negotiateEndpoint)
            {
                _negotiateEndpoint = negotiateEndpoint;
            }

            public override ServiceEndpoint GetNegotiateEndpoint(HttpContext context, IEnumerable<ServiceEndpoint> primaryEndpoints)
            {
                var endpointName = context.Request.Query["endpoint"];
                return primaryEndpoints.First(e => e.Name == endpointName);
            }
        }

        private class CustomUserIdProvider : IUserIdProvider
        {
            public string GetUserId(HubConnectionContext connection)
            {
                return connection.GetHttpContext()?.User?.Claims?.First(c => c.Type == CustomClaimType)?.Value;
            }
        }

        private class NullUserIdProvider : IUserIdProvider
        {
            public string GetUserId(HubConnectionContext connection) => null;
        }

        private class ConnectionIdUserIdProvider : IUserIdProvider
        {
            public string GetUserId(HubConnectionContext connection) => connection.ConnectionId;
        }

        private class ConnectionAbortedTokenUserIdProvider : IUserIdProvider
        {
            public string GetUserId(HubConnectionContext connection) => connection.ConnectionAborted.IsCancellationRequested.ToString();
        }

        private class ItemsUserIdProvider : IUserIdProvider
        {
            public string GetUserId(HubConnectionContext connection) => connection.Items.ToString();
        }

        private class ProtocolUserIdProvider : IUserIdProvider
        {
            public string GetUserId(HubConnectionContext connection) => connection.Protocol.Name;
        }
    }
}
