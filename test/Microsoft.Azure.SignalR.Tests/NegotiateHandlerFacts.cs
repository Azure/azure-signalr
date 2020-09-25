// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// From AspNetCore 3.0 preview7, there's a break change in HubConnectionContext
// which will break cross reference bettwen NETCOREAPP3.0 to NETStandard2.0 SDK
// So skip this part of UT when target 2.0 only
#if (MULTIFRAMEWORK)

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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

        private static readonly JwtSecurityTokenHandler JwtSecurityTokenHandler = new JwtSecurityTokenHandler();

        [Theory]
        [InlineData(typeof(CustomUserIdProvider), CustomUserId)]
        [InlineData(typeof(NullUserIdProvider), null)]
        [InlineData(typeof(DefaultUserIdProvider), DefaultUserId)]
        public async Task GenerateNegotiateResponseWithUserId(Type type, string expectedUserId)
        {
            var config = new ConfigurationBuilder().Build();
            var serviceProvider = new ServiceCollection()
                .AddSignalR(o => o.EnableDetailedErrors = false)
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
            var negotiateResponse = await handler.Process(httpContext, "hub");

            Assert.NotNull(negotiateResponse);
            Assert.NotNull(negotiateResponse.Url);
            Assert.NotNull(negotiateResponse.AccessToken);
            Assert.Null(negotiateResponse.ConnectionId);
            Assert.Empty(negotiateResponse.AvailableTransports);

            var token = JwtSecurityTokenHandler.ReadJwtToken(negotiateResponse.AccessToken);
            Assert.Equal(expectedUserId, token.Claims.FirstOrDefault(x => x.Type == Constants.ClaimType.UserId)?.Value);
            Assert.Equal("custom", token.Claims.FirstOrDefault(x => x.Type == "custom")?.Value);
            Assert.Equal(TimeSpan.FromDays(1), token.ValidTo - token.ValidFrom);
            Assert.Null(token.Claims.FirstOrDefault(s => s.Type == Constants.ClaimType.ServerName));
            Assert.Null(token.Claims.FirstOrDefault(s => s.Type == Constants.ClaimType.ServerStickyMode));
            Assert.Null(token.Claims.FirstOrDefault(s => s.Type == Constants.ClaimType.EnableDetailedErrors));
        }

        [Fact]
        public async Task GenerateNegotiateResponseWithDiagnosticClient()
        {
            var config = new ConfigurationBuilder().Build();
            var serviceProvider = new ServiceCollection()
                .AddSignalR(o => o.EnableDetailedErrors = false)
                .AddAzureSignalR(
                o =>
                {
                    o.ConnectionString = DefaultConnectionString;
                    o.AccessTokenLifetime = TimeSpan.FromDays(1);
                    o.DiagnosticClientFilter = ctx => { return ctx.Request.Query["diag"].FirstOrDefault() != default; } ;
                })
                .Services
                .AddLogging()
                .AddSingleton<IConfiguration>(config)
                .BuildServiceProvider();

            var httpContext = new DefaultHttpContext();
            httpContext.Request.QueryString = new QueryString("?diag");
            var handler = serviceProvider.GetRequiredService<NegotiateHandler>();
            var negotiateResponse = await handler.Process(httpContext, "hub");

            Assert.NotNull(negotiateResponse);
            Assert.NotNull(negotiateResponse.AccessToken);

            var token = JwtSecurityTokenHandler.ReadJwtToken(negotiateResponse.AccessToken);
            var tc = token.Claims.FirstOrDefault(s => s.Type == Constants.ClaimType.DiagnosticClient);
            Assert.NotNull(tc);
            Assert.Equal("true", tc.Value);
        }

        [Fact]
        public async Task GenerateNegotiateResponseWithUserIdAndServerSticky()
        {
            var name = nameof(GenerateNegotiateResponseWithUserIdAndServerSticky);
            var serverNameProvider = new TestServerNameProvider(name);
            var config = new ConfigurationBuilder().Build();
            var serviceProvider = new ServiceCollection()
                .AddSignalR(o => o.EnableDetailedErrors = true)
                .AddAzureSignalR(
                o =>
                {
                    o.ServerStickyMode = ServerStickyMode.Required;
                    o.ConnectionString = DefaultConnectionString;
                    o.AccessTokenLifetime = TimeSpan.FromDays(1);
                })
                .Services
                .AddLogging()
                .AddSingleton<IConfiguration>(config)
                .AddSingleton(typeof(IUserIdProvider), typeof(DefaultUserIdProvider))
                .AddSingleton(typeof(IServerNameProvider), serverNameProvider)
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
            var negotiateResponse = await handler.Process(httpContext, "hub");

            Assert.NotNull(negotiateResponse);
            Assert.NotNull(negotiateResponse.Url);
            Assert.NotNull(negotiateResponse.AccessToken);
            Assert.Null(negotiateResponse.ConnectionId);
            Assert.Empty(negotiateResponse.AvailableTransports);

            var token = JwtSecurityTokenHandler.ReadJwtToken(negotiateResponse.AccessToken);
            Assert.Equal(DefaultUserId, token.Claims.FirstOrDefault(x => x.Type == Constants.ClaimType.UserId)?.Value);
            Assert.Equal("custom", token.Claims.FirstOrDefault(x => x.Type == "custom")?.Value);
            Assert.Equal(TimeSpan.FromDays(1), token.ValidTo - token.ValidFrom);

            var serverName = token.Claims.FirstOrDefault(s => s.Type == Constants.ClaimType.ServerName)?.Value;
            Assert.Equal(name, serverName);
            var mode = token.Claims.FirstOrDefault(s => s.Type == Constants.ClaimType.ServerStickyMode)?.Value;
            Assert.Equal("Required", mode);
            Assert.Equal("True", token.Claims.FirstOrDefault(s => s.Type == Constants.ClaimType.EnableDetailedErrors)?.Value);
        }

        [Theory]
        [InlineData("/user/path/negotiate", "", "", "asrs.op=%2Fuser%2Fpath&asrs_request_id=")]
        [InlineData("/user/path/negotiate/", "", "a", "asrs.op=%2Fuser%2Fpath&asrs_request_id=a")]
        [InlineData("", "?customKey=customeValue", "?a=c", "customKey=customeValue&asrs_request_id=%3Fa%3Dc")]
        [InlineData("/user/path/negotiate", "?customKey=customeValue", "&", "asrs.op=%2Fuser%2Fpath&customKey=customeValue&asrs_request_id=%26")]
        public async Task GenerateNegotiateResponseWithPathAndQuery(string path, string queryString, string id, string expectedQueryString)
        {
            var requestIdProvider = new TestRequestIdProvider(id);
            var config = new ConfigurationBuilder().Build();
            var serviceProvider = new ServiceCollection().AddSignalR()
                .AddAzureSignalR(o => o.ConnectionString = DefaultConnectionString)
                .Services
                .AddLogging()
                .AddSingleton<IConfiguration>(config)
                .AddSingleton<IConnectionRequestIdProvider>(requestIdProvider)
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
            var negotiateResponse = await handler.Process(httpContext, "chat");

            Assert.NotNull(negotiateResponse);
            Assert.EndsWith($"?hub=chat&{expectedQueryString}", negotiateResponse.Url);
        }

        [Theory]
        [InlineData("", "&", "?hub=chat&asrs_request_id=%26")]
        [InlineData("appName", "abc", "?hub=appname_chat&asrs_request_id=abc")]
        public async Task GenerateNegotiateResponseWithAppName(string appName, string id, string expectedResponse)
        {
            var requestIdProvider = new TestRequestIdProvider(id);
            var config = new ConfigurationBuilder().Build();
            var serviceProvider = new ServiceCollection().AddSignalR()
                .AddAzureSignalR(o =>
                {
                    o.ConnectionString = DefaultConnectionString;
                    o.ApplicationName = appName;
                })
                .Services
                .AddLogging()
                .AddSingleton<IConfiguration>(config)
                .AddSingleton<IConnectionRequestIdProvider>(requestIdProvider)
                .BuildServiceProvider();

            var requestFeature = new HttpRequestFeature
            {
            };
            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(requestFeature);
            var httpContext = new DefaultHttpContext(features);

            var handler = serviceProvider.GetRequiredService<NegotiateHandler>();
            var negotiateResponse = await handler.Process(httpContext, "chat");

            Assert.NotNull(negotiateResponse);
            Assert.EndsWith(expectedResponse, negotiateResponse.Url);
        }

        [Theory]
        [InlineData(typeof(ConnectionIdUserIdProvider), ServiceHubConnectionContext.ConnectionIdUnavailableError)]
        [InlineData(typeof(ConnectionAbortedTokenUserIdProvider), ServiceHubConnectionContext.ConnectionAbortedUnavailableError)]
        [InlineData(typeof(ItemsUserIdProvider), ServiceHubConnectionContext.ItemsUnavailableError)]
        [InlineData(typeof(ProtocolUserIdProvider), ServiceHubConnectionContext.ProtocolUnavailableError)]
        public async Task CustomUserIdProviderAccessUnavailablePropertyThrowsException(Type type, string errorMessage)
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

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await handler.Process(httpContext, "hub"));
            Assert.Equal(errorMessage, exception.Message);
        }

        [Fact]
        public async Task TestNegotiateHandlerWithMultipleEndpointsAndCustomerRouterAndAppName()
        {
            var requestIdProvider = new TestRequestIdProvider("a");
            var config = new ConfigurationBuilder().Build();
            var router = new TestCustomRouter();
            var serviceProvider = new ServiceCollection().AddSignalR()
                .AddAzureSignalR(o =>
                {
                    o.ApplicationName = "testprefix";
                    o.Endpoints = new ServiceEndpoint[]
                    {
                        new ServiceEndpoint(ConnectionString2),
                        new ServiceEndpoint(ConnectionString3, name: "chosen"),
                        new ServiceEndpoint(ConnectionString4),
                    };
                })
                .Services
                .AddLogging()
                .AddSingleton<IEndpointRouter>(router)
                .AddSingleton<IConfiguration>(config)
                .AddSingleton<IConnectionRequestIdProvider>(requestIdProvider)
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
            var negotiateResponse = await handler.Process(httpContext, "chat");

            Assert.NotNull(negotiateResponse);
            Assert.Equal($"http://localhost3/client/?hub=testprefix_chat&asrs.op=%2Fuser%2Fpath&endpoint=chosen&asrs_request_id=a", negotiateResponse.Url);
        }

        [Fact]
        public async Task TestNegotiateHandlerWithMultipleEndpointsAndCustomRouter()
        {
            var requestIdProvider = new TestRequestIdProvider("a");
            var config = new ConfigurationBuilder().Build();
            var router = new TestCustomRouter();
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
                .AddSingleton<IConnectionRequestIdProvider>(requestIdProvider)
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
            var negotiateResponse = await handler.Process(httpContext, "chat");

            Assert.NotNull(negotiateResponse);
            Assert.Equal($"http://localhost3/client/?hub=chat&asrs.op=%2Fuser%2Fpath&endpoint=chosen&asrs_request_id=a", negotiateResponse.Url);

            // With no query string should return 400
            requestFeature = new HttpRequestFeature
            {
                Path = "/user/path/negotiate/",
            };

            var responseFeature = new HttpResponseFeature();
            features.Set<IHttpRequestFeature>(requestFeature);
            features.Set<IHttpResponseFeature>(responseFeature);
            httpContext = new DefaultHttpContext(features);

            handler = serviceProvider.GetRequiredService<NegotiateHandler>();
            negotiateResponse = await handler.Process(httpContext, "chat");

            Assert.Null(negotiateResponse);

            Assert.Equal(400, responseFeature.StatusCode);

            // With no query string should return 400
            requestFeature = new HttpRequestFeature
            {
                Path = "/user/path/negotiate/",
                QueryString = "?endpoint=notexists"
            };

            responseFeature = new HttpResponseFeature();
            features.Set<IHttpRequestFeature>(requestFeature);
            features.Set<IHttpResponseFeature>(responseFeature);
            httpContext = new DefaultHttpContext(features);

            handler = serviceProvider.GetRequiredService<NegotiateHandler>();
            await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Process(httpContext, "chat"));
        }

        [Fact]
        public async Task TestNegotiateHandlerRespectClientRequestCulture()
        {
            var config = new ConfigurationBuilder().Build();
            var serviceProvider = new ServiceCollection()
                .AddSignalR(o => o.EnableDetailedErrors = false)
                .AddAzureSignalR(
                o =>
                {
                    o.ConnectionString = DefaultConnectionString;
                    o.AccessTokenLifetime = TimeSpan.FromDays(1);
                })
                .Services
                .AddLogging()
                .AddSingleton<IConfiguration>(config)
                .BuildServiceProvider();

            var features = new FeatureCollection();
            var requestFeature = new HttpRequestFeature
            {
                Path = "/user/path/negotiate/",
                QueryString = "?endpoint=chosen"
            };
            features.Set<IHttpRequestFeature>(requestFeature);
            var customCulture = new RequestCulture("ar-SA");
            features.Set<IRequestCultureFeature>(
                new RequestCultureFeature(customCulture,
                new AcceptLanguageHeaderRequestCultureProvider()));

            var httpContext = new DefaultHttpContext(features);

            var handler = serviceProvider.GetRequiredService<NegotiateHandler>();
            var negotiateResponse = await handler.Process(httpContext, "hub");

            var queryContainsCulture = negotiateResponse.Url.Contains($"{Constants.QueryParameter.RequestCulture}=ar-SA");
            Assert.True(queryContainsCulture);
        }

        [Theory]
        [InlineData(-10)]
        [InlineData(0)]
        [InlineData(500)]
        public void TestInvalidDisconnectTimeoutThrowsAfterBuild(int maxPollInterval)
        {
            var config = new ConfigurationBuilder().Build();
            var serviceProvider = new ServiceCollection().AddSignalR()
                .AddAzureSignalR(o =>
                {
                    o.ConnectionString = DefaultConnectionString;
                    o.MaxPollIntervalInSeconds = maxPollInterval;
                })
                .Services
                .AddLogging()
                .AddSingleton<IConfiguration>(config)
                .BuildServiceProvider();

            Assert.Throws<AzureSignalRInvalidServiceOptionsException>(() => serviceProvider.GetRequiredService<NegotiateHandler>());
        }

        [Theory]
        [InlineData(1)]
        [InlineData(15)]
        [InlineData(300)]
        public async Task TestNegotiateHandlerResponseContainsValidMaxPollInterval(int maxPollInterval)
        {
            var config = new ConfigurationBuilder().Build();
            var serviceProvider = new ServiceCollection().AddSignalR()
                .AddAzureSignalR(o =>
                {
                    o.ConnectionString = DefaultConnectionString;
                    o.MaxPollIntervalInSeconds = maxPollInterval;
                })
                .Services
                .AddLogging()
                .AddSingleton<IConfiguration>(config)
                .BuildServiceProvider();

            var requestFeature = new HttpRequestFeature
            {
                Path = "/user/path/negotiate/",
                QueryString = "?endpoint=chosen"
            };
            var responseFeature = new HttpResponseFeature();
            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(requestFeature);
            features.Set<IHttpResponseFeature>(responseFeature);
            var httpContext = new DefaultHttpContext(features);

            var handler = serviceProvider.GetRequiredService<NegotiateHandler>();
            var response = await handler.Process(httpContext, "chat");

            Assert.Equal(200, responseFeature.StatusCode);

            var tokens = JwtTokenHelper.JwtHandler.ReadJwtToken(response.AccessToken);

            Assert.Contains(tokens.Claims, x => x.Type == Constants.ClaimType.MaxPollInterval && x.Value == maxPollInterval.ToString());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task TestNegotiateHandlerServerStickyRespectBlazor(bool isBlazor)
        {
            var hubName = nameof(TestNegotiateHandlerServerStickyRespectBlazor);
            var blazorDetector = new DefaultBlazorDetector();
            var config = new ConfigurationBuilder().Build();
            var serviceProvider = new ServiceCollection()
                .AddSignalR(o => o.EnableDetailedErrors = true)
                .AddAzureSignalR(
                o =>
                {
                    o.ServerStickyMode = ServerStickyMode.Preferred;
                    o.ConnectionString = DefaultConnectionString;
                })
                .Services
                .AddLogging()
                .AddSingleton<IConfiguration>(config)
                .AddSingleton(typeof(IUserIdProvider), typeof(DefaultUserIdProvider))
                .AddSingleton(typeof(IBlazorDetector), blazorDetector)
                .BuildServiceProvider();

            blazorDetector.TrySetBlazor(hubName, isBlazor);
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
            var negotiateResponse = await handler.Process(httpContext, hubName);

            Assert.NotNull(negotiateResponse);
            Assert.NotNull(negotiateResponse.Url);
            Assert.NotNull(negotiateResponse.AccessToken);
            Assert.Null(negotiateResponse.ConnectionId);
            Assert.Empty(negotiateResponse.AvailableTransports);

            var token = JwtSecurityTokenHandler.ReadJwtToken(negotiateResponse.AccessToken);

            var mode = token.Claims.FirstOrDefault(s => s.Type == Constants.ClaimType.ServerStickyMode)?.Value;
            if (isBlazor)
            {
                Assert.Equal("Required", mode);
            }
            else
            {
                Assert.Equal("Preferred", mode);
            }
            Assert.Equal("True", token.Claims.FirstOrDefault(s => s.Type == Constants.ClaimType.EnableDetailedErrors)?.Value);

        }

        private sealed class TestServerNameProvider : IServerNameProvider
        {
            private readonly string _serverName;
            public TestServerNameProvider(string serverName)
            {
                _serverName = serverName;
            }

            public string GetName()
            {
                return _serverName;
            }
        }

        private class TestCustomRouter : EndpointRouterDecorator
        {
            public override ServiceEndpoint GetNegotiateEndpoint(HttpContext context, IEnumerable<ServiceEndpoint> endpoints)
            {
                var endpointName = context.Request.Query["endpoint"];
                if (endpointName.Count == 0)
                {
                    context.Response.StatusCode = 400;
                    var response = Encoding.UTF8.GetBytes("Invalid request");
                    // In latest DefaultHttpContext, response body is set to null
                    context.Response.Body = new MemoryStream();
                    context.Response.Body.Write(response, 0, response.Length);
                    return null;
                }

                return endpoints.First(s => s.Name == endpointName && s.Online);
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

#endif