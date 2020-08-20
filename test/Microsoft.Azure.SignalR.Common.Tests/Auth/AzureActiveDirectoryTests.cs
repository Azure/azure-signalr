using System;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.Auth
{
    public class AzureActiveDirectoryTests
    {
        private const string IssuerEndpoint = "https://sts.windows.net/";

        private const string TestClientId = "";
        private const string TestClientSecret = "";
        private const string TestTenantId = "";

        [Fact]
        public void TestBuildAuthority()
        {
            Assert.Throws<FormatException>(() => new AadApplicationOptions("foo", ""));
            Assert.Throws<FormatException>(() => new AadApplicationOptions("foo", "An invalid tenant id"));
            Assert.Throws<FormatException>(() => new AadApplicationOptions("foo", ".00000000-0000-0000-0000-000000000000"));
            Assert.Throws<FormatException>(() => new AadApplicationOptions("foo", "00000000-0000-0000-0000-000000000000."));
            Assert.Throws<FormatException>(() => new AadApplicationOptions("foo", "00000000-0000-0000-θθθθ-000000000000"));
            Assert.Throws<FormatException>(() => new AadApplicationOptions("foo", "0000-0000-0000-θθθθθθθθ-000000000000"));
            Assert.Throws<FormatException>(() => new AadApplicationOptions("foo", "00000000-abcd-efgh-0000-000000000000"));
            Assert.Throws<FormatException>(() => new AadApplicationOptions("foo", "00000000-ABCD-EFGH-0000-000000000000"));

            AadApplicationOptions options;

            options = new AadApplicationOptions("foo", "00000000-0000-0000-0000-000000000000");
            Assert.Equal("https://login.microsoftonline.com/00000000-0000-0000-0000-000000000000", options.BuildAuthority().ToString());

            options = new AadApplicationOptions("foo", "00000000-abcd-efab-cdef-000000000000");
            options.WithDogfood();
            Assert.Equal("https://login.windows-ppe.net/00000000-abcd-efab-cdef-000000000000", options.BuildAuthority().ToString());
        }

        [Fact(Skip = "Provide valid aad options")]
        public async Task TestAcquireAccessToken()
        {
            var options = new AadApplicationOptions(TestClientId, TestTenantId).WithClientSecret(TestClientSecret);
            var token = await options.GenerateAccessToken();
        }

        [Fact(Skip = "Managed Identity required")]
        public async Task TestGetAzureAdTokenAndAuthenticate()
        {
            var options = new AadManagedIdentityOptions();

            ConfigurationManager<OpenIdConnectConfiguration> configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                options.BuildMetadataAddress().ToString(),
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

            var accessToken = await options.GenerateAccessToken();
            var claims = handler.ValidateToken(accessToken, p, out var validToken);

            Assert.NotNull(validToken);
        }
    }
}