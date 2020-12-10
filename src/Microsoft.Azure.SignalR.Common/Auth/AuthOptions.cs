﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal abstract class AuthOptions
    {
        internal const string Audience = "https://signalr.azure.com";

        internal const string ChinaInstance = "https://login.chinacloudapi.cn/";

        internal const string DogfoodInstance = "https://login.windows-ppe.net/";

        internal const string GermanyInstance = "https://login.microsoftonline.de/";

        internal const string GlobalInstance = "https://login.microsoftonline.com/";

        internal const string USGovernmentInstance = "https://login.microsoftonline.us/";

        internal abstract string AuthType { get; }

        protected string AzureActiveDirectoryInstance { get; set; } = GlobalInstance;

        public abstract Task<string> AcquireAccessToken();

        public AuthOptions WithChina()
        {
            AzureActiveDirectoryInstance = ChinaInstance;
            return this;
        }

        public AuthOptions WithDogfood()
        {
            AzureActiveDirectoryInstance = DogfoodInstance;
            return this;
        }

        public AuthOptions WithGermany()
        {
            AzureActiveDirectoryInstance = GermanyInstance;
            return this;
        }

        public AuthOptions WithGlobal()
        {
            AzureActiveDirectoryInstance = GlobalInstance;
            return this;
        }

        public AuthOptions WithUSGovernment()
        {
            AzureActiveDirectoryInstance = USGovernmentInstance;
            return this;
        }

        internal Uri BuildMetadataAddress()
        {
            return GetUri(AzureActiveDirectoryInstance, "common/v2.0/.well-known/openid-configuration");
        }

        protected Uri GetUri(string baseUri, string path) => new Uri(new Uri(baseUri), path);
    }
}
