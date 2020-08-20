// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    public class AadApplicationOptions : AuthOptions, IAadTokenGenerator
    {
        private static readonly string[] DefaultScopes = new string[] { $"{Audience}/.default" };

        public X509Certificate2 ClientCert { get; private set; }

        public string ClientId { get; }

        public string ClientSecret { get; private set; }

        public string TenantId { get; }

        internal override string AuthType => "AzureActiveDirectory";

        public AadApplicationOptions(string clientId, string tenantId)
        {
            if (!Guid.TryParseExact(tenantId, "D", out _))
            {
                throw new FormatException($"{tenantId} is not a valid tenandId, should be [xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx]");
            }
            TenantId = tenantId;
            ClientId = clientId;
        }

        public AadApplicationOptions WithClientSecret(string secret)
        {
            ClientSecret = secret;
            return this;
        }

        public AadApplicationOptions WithClientCert(X509Certificate2 cert)
        {
            ClientCert = cert;
            return this;
        }

        public Uri BuildAuthority()
        {
            return GetUri(AzureActiveDirectoryInstance, TenantId);
        }

        public async Task<string> GenerateAccessToken()
        {
            var result = await AzureActiveDirectoryHelper.BuildApplication(this).AcquireTokenForClient(DefaultScopes).WithSendX5C(true).ExecuteAsync();
            return result.AccessToken;
        }
    }
}