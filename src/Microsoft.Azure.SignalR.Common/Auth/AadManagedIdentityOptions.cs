using System.Threading.Tasks;
using Microsoft.Azure.Services.AppAuthentication;

namespace Microsoft.Azure.SignalR
{
    public class AadManagedIdentityOptions : AuthOptions, IAadTokenGenerator
    {
        internal override string AuthType => "ManagedIdentity";

        public async Task<string> GenerateAccessToken()
        {
            var azureServiceTokenProvider = new AzureServiceTokenProvider(azureAdInstance: AzureActiveDirectoryInstance);
            return await azureServiceTokenProvider.GetAccessTokenAsync(Audience);
        }
    }
}
