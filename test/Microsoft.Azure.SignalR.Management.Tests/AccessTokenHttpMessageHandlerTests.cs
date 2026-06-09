// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.IdentityModel.Tokens;

using Moq;

using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class AccessTokenHttpMessageHandlerTests
    {
        [Fact]
        public async Task SendAsync_ShouldSetAuthorizationHeader()
        {
            var key = FakeEndpointUtils.FakeAccessKey;
            var mockServiceEndpointManager = new Mock<IServiceEndpointManager>();
            var mockEndpoint = new ServiceEndpoint($"Endpoint=https://test.service.signalr.net;AccessKey={key};Version=1.0;");
            mockServiceEndpointManager.Setup(manager => manager.Endpoints).Returns(new Dictionary<ServiceEndpoint, ServiceEndpoint> { { mockEndpoint, mockEndpoint } });

            var handler = new AccessTokenHttpMessageHandler(mockServiceEndpointManager.Object, Mock.Of<IServerNameProvider>(p=>p.GetName()=="servername"))
            {
                InnerHandler = new TestRootHandler(HttpStatusCode.OK)
            };

            var httpClient = new HttpClient(handler);
            var request = new HttpRequestMessage(HttpMethod.Get, "https://abc/api/test?key=value");

            // Act
            var response = await httpClient.SendAsync(request);

            // Assert
            Assert.NotNull(request.Headers.Authorization);
            Assert.Equal("Bearer", request.Headers.Authorization.Scheme);
            var jwtHandler = new JwtSecurityTokenHandler();

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = Constants.AsrsTokenIssuer, // Replace with the expected issuer

                ValidateAudience = true,
                ValidAudience = "https://test.service.signalr.net/api/test",

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),

                ValidateLifetime = true, // Validate token expiration
                ClockSkew = TimeSpan.Zero // Optional: Adjust for clock skew
            };

            var principal = jwtHandler.ValidateToken(request.Headers.Authorization.Parameter, validationParameters, out var validatedToken);
            Assert.Contains(principal.Claims, c => c.Type == ClaimTypes.NameIdentifier && c.Value == "servername");
        }
    }
}
