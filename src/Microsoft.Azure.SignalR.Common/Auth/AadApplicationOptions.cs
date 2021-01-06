// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

namespace Microsoft.Azure.SignalR
{
    internal class AadApplicationOptions : AuthOptions, IAadTokenGenerator
    {
        private static readonly string[] DefaultScopes = new string[] { $"{Audience}/.default" };

        public X509Certificate2 ClientCert { get; private set; }

        public string ClientId { get; }

        public string ClientSecret { get; private set; }

        public string TenantId { get; }

        public IConfidentialClientApplication Application { get; private set; } = null;

        internal override string AuthType => "AzureActiveDirectory";

        public AadApplicationOptions(string clientId, string tenantId)
        {
            if (!Guid.TryParseExact(tenantId, "D", out _))
            {
                throw new FormatException($"The given tenantId \"{tenantId}\" is not a valid guid.");
            }
            TenantId = tenantId;
            ClientId = clientId;
        }

        public AadApplicationOptions(IConfidentialClientApplication app)
        {
            Application = app;
        }

        public AadApplicationOptions WithClientSecret(string secret)
        {
            ClientSecret = secret;
            Application = AzureActiveDirectoryHelper.BuildApplication(this);
            return this;
        }

        public AadApplicationOptions WithClientCert(X509Certificate2 cert)
        {
            ClientCert = cert;
            Application = AzureActiveDirectoryHelper.BuildApplication(this);
            return this;
        }

        public Uri BuildAuthority()
        {
            return GetUri(AzureActiveDirectoryInstance, TenantId);
        }

        public override async Task<string> AcquireAccessToken()
        {
            var result = await Application.AcquireTokenForClient(DefaultScopes).WithSendX5C(true).ExecuteAsync();
            return result.AccessToken;
        }
    }
}