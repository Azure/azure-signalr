// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Azure.Services.AppAuthentication;

namespace Microsoft.Azure.SignalR
{
    public class AzureActiveDirectoryOptions : AuthOptions
    {
        internal const string Audience = "https://signalr.azure.com";

        internal const string ChinaInstance = "https://login.chinacloudapi.cn/";

        internal const string DogfoodInstance = "https://login.windows-ppe.net/";

        internal const string GermanyInstance = "https://login.microsoftonline.de/";

        internal const string GlobalInstance = "https://login.microsoftonline.com/";

        internal const string USGovernmentInstance = "https://login.microsoftonline.us/";

        private static readonly string[] DefaultScopes = new string[] { $"{Audience}/.default" };

        private readonly TokenKind _tokenMethod;

        public X509Certificate2 ClientCert { get; }

        public string ClientId { get; }

        public string ClientSecret { get; }

        public string TenantId { get; }

        internal override string AuthType => "AzureActiveDirectory";

        private string AzureActiveDirectoryInstance { get; set; } = GlobalInstance;

        public AzureActiveDirectoryOptions()
        {
            _tokenMethod = TokenKind.FromManagedIdentity;
        }

        public AzureActiveDirectoryOptions(string clientId, string clientSecret, string tenantId) : this(clientId, tenantId)
        {
            _tokenMethod = TokenKind.FromClientSecret;

            ClientSecret = clientSecret;
        }

        public AzureActiveDirectoryOptions(string clientId, X509Certificate2 clientCert, string tenantId) : this(clientId, tenantId)
        {
            _tokenMethod = TokenKind.FromClientCert;

            ClientCert = clientCert;
        }

        private AzureActiveDirectoryOptions(string clientId, string tenantId)
        {
            if (!Guid.TryParseExact(tenantId, "D", out _))
            {
                throw new FormatException($"{tenantId} is not a valid tenandId, should be [xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx]");
            }
            TenantId = tenantId;
            ClientId = clientId;
        }

        private enum TokenKind
        {
            FromManagedIdentity,
            FromClientSecret,
            FromClientCert,
        }

        public async Task<string> AcquireAccessToken()
        {
            switch (_tokenMethod)
            {
                case TokenKind.FromManagedIdentity:
                    var azureServiceTokenProvider = new AzureServiceTokenProvider(azureAdInstance: AzureActiveDirectoryInstance);
                    return await azureServiceTokenProvider.GetAccessTokenAsync(Audience);
                case TokenKind.FromClientCert:
                case TokenKind.FromClientSecret:
                    var result = await AzureActiveDirectoryHelper.BuildApplication(this).AcquireTokenForClient(DefaultScopes).WithSendX5C(true).ExecuteAsync();
                    return result.AccessToken;
                default:
                    throw new NotSupportedException();
            }
        }

        public Uri BuildAuthority()
        {
            return GetUri(AzureActiveDirectoryInstance, TenantId);
        }

        public AzureActiveDirectoryOptions WithChina()
        {
            AzureActiveDirectoryInstance = ChinaInstance;
            return this;
        }

        public AzureActiveDirectoryOptions WithDogfood()
        {
            AzureActiveDirectoryInstance = DogfoodInstance;
            return this;
        }

        public AzureActiveDirectoryOptions WithGermany()
        {
            AzureActiveDirectoryInstance = GermanyInstance;
            return this;
        }

        public AzureActiveDirectoryOptions WithGlobal()
        {
            AzureActiveDirectoryInstance = GlobalInstance;
            return this;
        }

        public AzureActiveDirectoryOptions WithUSGovernment()
        {
            AzureActiveDirectoryInstance = USGovernmentInstance;
            return this;
        }

        internal Uri BuildMetadataAddress()
        {
            return GetUri(AzureActiveDirectoryInstance, "common/v2.0/.well-known/openid-configuration");
        }

        private Uri GetUri(string baseUri, string path) => new Uri(new Uri(baseUri), path);
    }
}