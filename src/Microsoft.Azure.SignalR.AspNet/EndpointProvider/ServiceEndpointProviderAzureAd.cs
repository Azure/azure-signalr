// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class ServiceEndpointProviderAzureAd : ServiceEndpointProvider
    {
        private readonly AadAccessKey _key;

        public ServiceEndpointProviderAzureAd(ServiceEndpoint endpoint, ServiceOptions options) : base(endpoint, options)
        {
            _key = endpoint.AccessKey is AadAccessKey key ? key : throw new ArgumentException("123");
        }

        public override Task<string> GenerateServerAccessTokenAsync(string hubName, string userId, TimeSpan? lifetime = null)
        {
            return _key.GenerateAadTokenAsync();
        }
    }
}
