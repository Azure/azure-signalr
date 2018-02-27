// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR
{
    public class HubProxyOptions
    {
        public const string DefaultApiVersion = "v1-preview";
        public static HubProxyOptions DefaultHubProxyOptions = new HubProxyOptions();

        public string ApiVersion { get; set; } = DefaultApiVersion;
    }
}
