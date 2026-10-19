// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.Http;
using System.Runtime.CompilerServices;

namespace Microsoft.Azure.SignalR;

// One store per server or ServiceManager, shared with its hub contexts.
internal sealed class ServiceEndpointAccessKeyStore
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ConditionalWeakTable<ServiceEndpoint, IAccessKey> _keys = new();

    public ServiceEndpointAccessKeyStore(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public IAccessKey GetAccessKey(ServiceEndpoint endpoint)
    {
        if (endpoint is HubServiceEndpoint hubEndpoint)
        {
            endpoint = hubEndpoint.EndpointConfiguration;
        }
        return _keys.GetValue(endpoint, e => string.IsNullOrEmpty(e.AccessKeyValue)
            ? new MicrosoftEntraAccessKey(e.ServerEndpoint, e.TokenCredential!, _httpClientFactory)
            : new AccessKey(e.AccessKeyValue));
    }
}
