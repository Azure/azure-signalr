// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Microsoft.Azure.SignalR.AspNet.Tests;

#nullable enable

public class ConnectionFactoryTests
{
    [Fact]
    public void TestGetRequestHeaders()
    {
        var nameProvider = new DefaultServerNameProvider();

        var loggerFactory = NullLoggerFactory.Instance;

        var factory = new ConnectionFactory(nameProvider, loggerFactory);

        var headers = factory.GetRequestHeaders();
        Assert.True(headers.TryGetValue(Constants.AsrsUserAgent, out var productInfo));
        Assert.StartsWith("Microsoft.Azure.SignalR.AspNet/", productInfo);

        Assert.True(headers.TryGetValue(Constants.Headers.AsrsServerId, out var serverId));
        Assert.Equal(nameProvider.GetName(), serverId);
    }

    [Fact]
    public void TestGetServerIdInRequestHeaders()
    {
        var nameProvider1 = new DefaultServerNameProvider();
        var nameProvider2 = new DefaultServerNameProvider();

        var name1 = nameProvider1.GetName();
        var name2 = nameProvider2.GetName();
        Assert.NotEqual(name1, name2);

        static string GetServerId(IServerNameProvider nameProvider)
        {
            var connectionFactory = new ConnectionFactory(nameProvider, NullLoggerFactory.Instance);
            var headers = connectionFactory.GetRequestHeaders();
            Assert.True(headers.TryGetValue(Constants.Headers.AsrsServerId, out var serverId));
            return serverId;
        }

        var serverId1 = GetServerId(nameProvider1);
        var serverId2 = GetServerId(nameProvider2);
        Assert.NotEqual(serverId1, serverId2);
    }
}
