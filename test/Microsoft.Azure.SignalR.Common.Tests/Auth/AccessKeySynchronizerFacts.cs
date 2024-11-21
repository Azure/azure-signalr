// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Azure.Identity;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.Auth;

public class AccessKeySynchronizerFacts
{
    [Fact]
    public void AddAndRemoveServiceEndpointsTest()
    {
        var synchronizer = GetInstanceForTest();

        var credential = new DefaultAzureCredential();
        var endpoint1 = new TestServiceEndpoint(credential);
        var endpoint2 = new TestServiceEndpoint(credential);

        Assert.Equal(0, synchronizer.Count());
        synchronizer.UpdateServiceEndpoints([endpoint1]);
        Assert.Equal(1, synchronizer.Count());
        synchronizer.UpdateServiceEndpoints([endpoint1, endpoint2]);
        Assert.Empty(synchronizer.InitializedKeyList);

        Assert.Equal(2, synchronizer.Count());
        Assert.True(synchronizer.ContainsKey(endpoint1));
        Assert.True(synchronizer.ContainsKey(endpoint2));

        synchronizer.UpdateServiceEndpoints([endpoint2]);
        Assert.Equal(1, synchronizer.Count());
        synchronizer.UpdateServiceEndpoints([]);
        Assert.Equal(0, synchronizer.Count());
        Assert.Empty(synchronizer.InitializedKeyList);
    }

    private static AccessKeySynchronizer GetInstanceForTest()
    {
        return new AccessKeySynchronizer(NullLoggerFactory.Instance, false);
    }
}
