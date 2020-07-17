// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR
{
    public abstract class AuthOptions
    {
        internal const string Audience = "https://signalr.azure.com";

        internal const string ChinaInstance = "https://login.chinacloudapi.cn/";

        internal const string DogfoodInstance = "https://login.windows-ppe.net/";

        internal const string GermanyInstance = "https://login.microsoftonline.de/";

        internal const string GlobalInstance = "https://login.microsoftonline.com/";

        internal const string USGovernmentInstance = "https://login.microsoftonline.us/";

        protected string AzureActiveDirectoryInstance { get; set; } = GlobalInstance;

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

        internal abstract string AuthType { get; }
    }
}
