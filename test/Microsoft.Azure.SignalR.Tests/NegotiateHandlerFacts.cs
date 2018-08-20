// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class NegotiateHandlerFacts
    {
        private static readonly JwtSecurityTokenHandler JwtSecurityTokenHandler = new JwtSecurityTokenHandler();
        private const string DefaultConnectionString = "Endpoint=https://localhost;AccessKey=nOu3jXsHnsO5urMumc87M9skQbUWuQ+PE5IvSUEic8w=;";

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GenerateNegotiateResponse(bool isDefaultUserIdProvider)
        {
            var config = new ConfigurationBuilder().Build();
            var services = new ServiceCollection().AddSignalR()
                .AddAzureSignalR(o => o.ConnectionString = DefaultConnectionString)
                .Services
                .AddSingleton<IConfiguration>(config);

            if (!isDefaultUserIdProvider)
            {
                services.AddSingleton<IUserIdProvider, TestUserIdProvider>();
            }

            var serviceProvider = services.BuildServiceProvider();

            var handler = serviceProvider.GetRequiredService<NegotiateHandler>();
            var httpContext = new DefaultHttpContext {User = new ClaimsPrincipal()};
            var negotiateReponse = handler.Process(httpContext, "hub");

            Assert.NotNull(negotiateReponse);
            Assert.NotNull(negotiateReponse.Url);
            Assert.NotNull(negotiateReponse.AccessToken);
            Assert.Null(negotiateReponse.ConnectionId);
            Assert.Empty(negotiateReponse.AvailableTransports);

            var token = JwtSecurityTokenHandler.ReadJwtToken(negotiateReponse.AccessToken);
            var customUserIdClaimCount = isDefaultUserIdProvider ? 0 : 1;
            Assert.Equal(customUserIdClaimCount, token.Claims.Count(x => x.Type == Constants.ClaimType.UserId));
        }

        private class TestUserIdProvider : IUserIdProvider
        {
            public string GetUserId(HubConnectionContext connection)
            {
                throw new NotImplementedException();
            }
        }
    }
}
