using System.Threading.Tasks;
using Microsoft.Azure.Services.AppAuthentication;

namespace Microsoft.Azure.SignalR
{
    internal class AadManagedIdentityOptions : AuthOptions, IAadTokenGenerator
    {
        private readonly AzureServiceTokenProvider _azureServiceTokenProvider;

        internal override string AuthType => "ManagedIdentity";

        internal ManagedIdentityType ManagedIdentityType { get; }

        public AadManagedIdentityOptions()
        {
            _azureServiceTokenProvider = new AzureServiceTokenProvider("RunAs=App", AzureActiveDirectoryInstance);
            ManagedIdentityType = ManagedIdentityType.System;
        }

        public AadManagedIdentityOptions(string clientId)
        {
            _azureServiceTokenProvider = new AzureServiceTokenProvider($"RunAs=App;AppId={clientId}", AzureActiveDirectoryInstance);
            ManagedIdentityType = ManagedIdentityType.UserAssigned;
        }

        public override async Task<string> AcquireAccessToken()
        {
            return await _azureServiceTokenProvider.GetAccessTokenAsync(Audience);
        }
    }
}
