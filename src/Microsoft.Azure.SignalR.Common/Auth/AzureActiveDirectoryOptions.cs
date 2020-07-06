// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Azure.SignalR
{
    public class AzureActiveDirectoryOptions : AuthOptions
    {
        public static string[] DefaultScopes = new string[] { "https://signalr.azure.com/.default" };

        internal const string ChinaFormat = "https://login.chinacloudapi.cn/{0}";
        internal const string DogfoodFormat = "https://login.windows-ppe.net/{0}";
        internal const string GermanyFormat = "https://login.microsoftonline.de/{0}";
        internal const string GlobalFormat = "https://login.microsoftonline.com/{0}";
        internal const string USGovernmentFormat = "https://login.microsoftonline.us/{0}";

        private string _format = GlobalFormat;

        public X509Certificate2 ClientCert { get; }

        public string ClientId { get; }
        public string ClientSecret { get; }
        public string TenantId { get; }

        internal override string AuthType => "AzureActiveDirectory";

        public AzureActiveDirectoryOptions(string clientId, string clientSecret, string tenantId) : this(clientId, tenantId)
        {
            ClientSecret = clientSecret;
        }

        public AzureActiveDirectoryOptions(string clientId, X509Certificate2 clientCert, string tenantId) : this(clientId, tenantId)
        {
            ClientCert = clientCert;
        }

        private AzureActiveDirectoryOptions()
        {
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

        public Uri BuildAuthority()
        {
            string uri = string.Format(_format, TenantId);
            return new Uri(uri);
        }

        public AzureActiveDirectoryOptions WithChina()
        {
            _format = ChinaFormat;
            return this;
        }

        public AzureActiveDirectoryOptions WithDogfood()
        {
            _format = DogfoodFormat;
            return this;
        }

        public AzureActiveDirectoryOptions WithGermany()
        {
            _format = GermanyFormat;
            return this;
        }

        public AzureActiveDirectoryOptions WithGlobal()
        {
            _format = GlobalFormat;
            return this;
        }

        public AzureActiveDirectoryOptions WithUSGovernment()
        {
            _format = USGovernmentFormat;
            return this;
        }

        internal string BuildMetadataAddress()
        {
            return string.Format(_format, "common/v2.0/.well-known/openid-configuration");
        }
    }
}