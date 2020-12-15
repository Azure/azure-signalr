using System.Threading.Tasks;
using Microsoft.Azure.Services.AppAuthentication;

namespace Microsoft.Azure.SignalR
{
    internal class AadManagedIdentityOptions : AuthOptions, IAadTokenGenerator
    {
        internal override string AuthType => "ManagedIdentity";

        public override async Task<string> AcquireAccessToken()
        {
            var azureServiceTokenProvider = new AzureServiceTokenProvider(azureAdInstance: AzureActiveDirectoryInstance);
            return await azureServiceTokenProvider.GetAccessTokenAsync(Audience);
        }
    }
}
