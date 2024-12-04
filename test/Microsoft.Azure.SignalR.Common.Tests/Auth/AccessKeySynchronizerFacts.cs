// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.Azure.SignalR.Tests.Common;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.Auth;

public class AccessKeySynchronizerFacts
{
    [Fact]
    public async void AddRemoveServiceEndpointTest()
    {
        var synchronizer = new AccessKeySynchronizer();
        Assert.Equal(0, synchronizer.Count);

        var credential = new DefaultAzureCredential();
        var endpoint = new TestServiceEndpoint(credential);
        synchronizer.AddServiceEndpoint(endpoint);
        Assert.Equal(1, synchronizer.Count);

        var field = typeof(MicrosoftEntraAccessKey).GetField("_lastUsedAt", BindingFlags.NonPublic | BindingFlags.Instance);

        var key = Assert.IsType<MicrosoftEntraAccessKey>(endpoint.AccessKey);
        var before = Assert.IsType<DateTime>(field.GetValue(key));

        var source = new CancellationTokenSource(1000);
        await Assert.ThrowsAsync<TaskCanceledException>(async () => await key.GenerateAccessTokenAsync("localhost", [], TimeSpan.FromHours(1), AccessTokenAlgorithm.HS256, source.Token));
        var after = Assert.IsType<DateTime>(field.GetValue(key));
        Assert.NotEqual(before, after);

        synchronizer.UpdateAllAccessKey();
        await Task.Delay(TimeSpan.FromSeconds(1));
        Assert.Equal(1, synchronizer.Count);

        key.UpdateAccessKey("foo", "bar");
        field.SetValue(key, DateTime.UtcNow - TimeSpan.FromHours(3));
        synchronizer.UpdateAllAccessKey();
        Assert.Equal(0, synchronizer.Count);
    }

    [Fact]
    public void HotReloadServiceEndpointTest()
    {
        var synchronizer = new AccessKeySynchronizer();

        var credential = new DefaultAzureCredential();
        var endpoint1 = new TestServiceEndpoint(credential);
        var endpoint2 = new TestServiceEndpoint(credential);

        Assert.Equal(0, synchronizer.Count);
        synchronizer.UpdateServiceEndpoints([endpoint1]);
        Assert.Equal(1, synchronizer.Count);
        synchronizer.UpdateServiceEndpoints([endpoint1, endpoint2]);
        Assert.Empty(synchronizer.InitializedKeyList);

        Assert.Equal(2, synchronizer.Count);
        Assert.True(synchronizer.ContainsKey(endpoint1));
        Assert.True(synchronizer.ContainsKey(endpoint2));

        synchronizer.UpdateServiceEndpoints([endpoint2]);
        Assert.Equal(1, synchronizer.Count);
        synchronizer.UpdateServiceEndpoints([]);
        Assert.Equal(0, synchronizer.Count);
        Assert.Empty(synchronizer.InitializedKeyList);
    }
}
