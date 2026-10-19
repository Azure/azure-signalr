// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Azure.Identity;

using Microsoft.Azure.SignalR.Tests.Common;

using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests;

public class ServiceEndpointAccessKeyStoreTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SharesKeyForSameConfigurationWithinStore(bool useEntra)
    {
        var endpoint = useEntra
            ? new TestServiceEndpoint(new DefaultAzureCredential())
            : new TestServiceEndpoint();
        var store = new ServiceEndpointAccessKeyStore(HttpClientFactory.Instance);
        var key = store.GetAccessKey(endpoint);
        var provider = new TestServiceEndpointProvider(key);
        var firstHub = new HubServiceEndpoint("first", provider, endpoint);
        var secondHub = new HubServiceEndpoint("second", provider, endpoint);

        Assert.Same(key, store.GetAccessKey(endpoint));
        Assert.Same(key, store.GetAccessKey(firstHub));
        Assert.Same(key, store.GetAccessKey(secondHub));

        var otherStore = new ServiceEndpointAccessKeyStore(HttpClientFactory.Instance);
        Assert.NotSame(key, otherStore.GetAccessKey(endpoint));
        Assert.NotSame(key, store.GetAccessKey(new ServiceEndpoint(endpoint)));
    }
}
