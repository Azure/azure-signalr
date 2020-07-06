using System;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Microsoft.Azure.SignalR.Common
{
    public class AzureActiveDirectoryTests
    {
        private const string IssuerEndpoint = "https://sts.windows.net/";

        [Fact(Skip="Provide a valid aad options")]
        public async Task TestGetAzureAdTokenAndAuthenticate()
        {
            var options = new AzureActiveDirectoryOptions(
                "<clientId>",
                "<clientSecret>",
                "<tenantId>"
            );

            var app = AzureActiveDirectoryHelper.BuildApplication(options);

            var token = await app.AcquireTokenForClient(AzureActiveDirectoryOptions.DefaultScopes).WithSendX5C(true).ExecuteAsync();

            ConfigurationManager<OpenIdConnectConfiguration> configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                options.BuildMetadataAddress(),
                new OpenIdConnectConfigurationRetriever()
            );
            var keys = configManager.GetConfigurationAsync().Result.SigningKeys;

            var p = new TokenValidationParameters()
            {
                ValidateLifetime = true,
                ValidateAudience = false,

                IssuerValidator = (string issuer, SecurityToken securityToken, TokenValidationParameters validationParameters) =>
                {
                    if (issuer.StartsWith(IssuerEndpoint)) {
                        return IssuerEndpoint;
                    }
                    throw new SecurityTokenInvalidIssuerException();
                },

                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = keys,
            };

            var handler = new JwtSecurityTokenHandler();
            IdentityModelEventSource.ShowPII = true;
            var claims = handler.ValidateToken(token.AccessToken, p, out var validToken);

            Assert.NotNull(validToken);
        }

        [Fact]
        public void TestBuildAuthority()
        {
            AzureActiveDirectoryOptions options;

            Assert.Throws<FormatException>(() => new AzureActiveDirectoryOptions("foo", "bar", ""));
            Assert.Throws<FormatException>(() => new AzureActiveDirectoryOptions("foo", "bar", "a invalid tenant id"));
            Assert.Throws<FormatException>(() => new AzureActiveDirectoryOptions("foo", "bar", ".00000000-0000-0000-0000-000000000000"));
            Assert.Throws<FormatException>(() => new AzureActiveDirectoryOptions("foo", "bar", "00000000-0000-0000-0000-000000000000."));
            Assert.Throws<FormatException>(() => new AzureActiveDirectoryOptions("foo", "bar", "00000000-0000-0000-θθθθ-000000000000"));
            Assert.Throws<FormatException>(() => new AzureActiveDirectoryOptions("foo", "bar", "0000-0000-0000-θθθθθθθθ-000000000000"));
            Assert.Throws<FormatException>(() => new AzureActiveDirectoryOptions("foo", "bar", "00000000-abcd-efgh-0000-000000000000"));
            Assert.Throws<FormatException>(() => new AzureActiveDirectoryOptions("foo", "bar", "00000000-ABCD-EFGH-0000-000000000000"));

            options = new AzureActiveDirectoryOptions("foo", "bar", "00000000-0000-0000-0000-000000000000");
            Assert.Equal("https://login.microsoftonline.com/00000000-0000-0000-0000-000000000000", options.BuildAuthority().ToString());

            options = new AzureActiveDirectoryOptions("foo", "bar", "00000000-abcd-efab-cdef-000000000000").WithDogfood();
            Assert.Equal("https://login.windows-ppe.net/00000000-abcd-efab-cdef-000000000000", options.BuildAuthority().ToString());
        }
    }
}