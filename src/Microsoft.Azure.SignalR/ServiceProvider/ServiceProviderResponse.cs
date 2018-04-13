// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Newtonsoft.Json;

namespace Microsoft.Azure.SignalR
{
    public class ServiceProviderResponse
    {
        public string ServiceUrl { get; set; }

        public string AccessToken { get; set; }
    }
}
