using System.Threading.Tasks;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.SignalR.Common.Auth;

namespace Microsoft.Azure.SignalR
{
    public class AadManagedIdentityOptions : AuthOptions, ITokenBasedAuthOptions
    {
        internal override string AuthType => "ManagedIdentity";

        public async Task<string> AcquireAccessToken()
        {
            var azureServiceTokenProvider = new AzureServiceTokenProvider(azureAdInstance: AzureActiveDirectoryInstance);
            return await azureServiceTokenProvider.GetAccessTokenAsync(Audience);
        }
    }
}
