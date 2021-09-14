﻿using System;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;

using Azure.Core;
using Azure.Identity;

using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.Auth
{
    [Collection("Auth")]
    public class AzureActiveDirectoryTests
    {
        private const string IssuerEndpoint = "https://sts.windows.net/";

        private const string TestClientId = "";
        private const string TestClientSecret = "";
        private const string TestTenantId = "";

        private static readonly string[] DefaultScopes = new string[] { "https://signalr.azure.com/.default" };

        [Fact(Skip = "Provide valid aad options")]
        public async Task TestAcquireAccessToken()
        {
            var options = new ClientSecretCredential(TestTenantId, TestClientId, TestClientSecret);
            var key = new AadAccessKey(new Uri("https://localhost:8080"), options);
            var token = await key.GenerateAadTokenAsync();
            Assert.NotNull(token);
        }

        [Fact(Skip = "Provide valid aad options")]
        public async Task TestGetAzureAdTokenAndAuthenticate()
        {
            var credential = new ClientSecretCredential(TestTenantId, TestClientId, TestClientSecret);

            ConfigurationManager<OpenIdConnectConfiguration> configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                "https://login.microsoftonline.com/common/v2.0/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever()
            );
            var keys = configManager.GetConfigurationAsync().Result.SigningKeys;

            var p = new TokenValidationParameters()
            {
                ValidateLifetime = true,
                ValidateAudience = false,

                IssuerValidator = (string issuer, SecurityToken securityToken, TokenValidationParameters validationParameters) =>
                {
                    if (issuer.StartsWith(IssuerEndpoint))
                    {
                        return IssuerEndpoint;
                    }
                    throw new SecurityTokenInvalidIssuerException();
                },

                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = keys,
            };

            var handler = new JwtSecurityTokenHandler();
            IdentityModelEventSource.ShowPII = true;

            var accessToken = await credential.GetTokenAsync(new TokenRequestContext(DefaultScopes));
            var claims = handler.ValidateToken(accessToken.Token, p, out var validToken);

            Assert.NotNull(validToken);
        }

        [Fact(Skip = "Provide valid aad options")]
        internal async Task TestAuthenticateAsync()
        {
            var options = new ClientSecretCredential(TestTenantId, TestClientId, TestClientSecret);
            var key = new AadAccessKey(new Uri("https://localhost:8080"), options);
            await key.UpdateAccessKeyAsync();

            Assert.True(key.Authorized);
            Assert.NotNull(key.Id);
            Assert.NotNull(key.Value);
        }
    }
}