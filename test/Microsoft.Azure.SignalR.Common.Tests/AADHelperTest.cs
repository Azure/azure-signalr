using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common.Utilities;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Microsoft.Azure.SignalR.Common
{
    public class AADHelperTest
    {
        [Fact]
        public async Task TestAsync()
        {
            var app = AadHelper.BuildApplication(new AzureAdOptions(
                "70f09175-ecf3-477e-ad90-bb5dec839250",
                "Y29V=Uw4@FUIX5gHfw?TmQRDyN=D:C-r",
                "c8a86907-dd80-4e5d-994d-36e0694e4913"
            ));

            string[] scopes = new string[] { "https://graph.microsoft.com/.default" };
            var token = await app.AcquireTokenForClient(scopes).WithSendX5C(true).ExecuteAsync();

            var uri = "https://login.microsoftonline.com/common/v2.0/.well-known/openid-configuration";
            ConfigurationManager<OpenIdConnectConfiguration> configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                uri,
                new OpenIdConnectConfigurationRetriever()
            );
            var keys = configManager.GetConfigurationAsync().Result.SigningKeys;

            var p = new TokenValidationParameters()
            {
                ValidateLifetime = false,
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateIssuerSigningKey = false,
                IssuerSigningKeys = keys,
            };

            var handler = new JwtSecurityTokenHandler();
            IdentityModelEventSource.ShowPII = true;
            handler.ValidateToken(token.AccessToken, p, out _);
        }
    }
}
