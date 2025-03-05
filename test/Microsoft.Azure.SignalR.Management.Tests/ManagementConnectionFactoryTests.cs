// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests;

#nullable enable

public class ManagementConnectionFactoryTests
{
    private const string OptionsAsrsUserAgent = $"Microsoft.Azure.SignalR.Foo/123456";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TestGetRequestHeaders(bool setManagementProductInfo)
    {
        var nameProvider = new DefaultServerNameProvider();

        var loggerFactory = NullLoggerFactory.Instance;

        var options = Options.Create(new ServiceManagerOptions());
        if (setManagementProductInfo)
        {
            options.Value.ProductInfo = OptionsAsrsUserAgent;
        }

        var factory = new ManagementConnectionFactory(options, nameProvider, loggerFactory);

        var headers = factory.GetRequestHeaders();
        Assert.True(headers.TryGetValue(Constants.AsrsUserAgent, out var productInfo));

        Assert.True(headers.TryGetValue(Constants.Headers.AsrsServerId, out var serverId));
        Assert.Equal(nameProvider.GetName(), serverId);

        if (setManagementProductInfo)
        {
            Assert.StartsWith("Microsoft.Azure.SignalR.Foo/", productInfo);
        }
        else
        {
            Assert.StartsWith("Microsoft.Azure.SignalR.Management/", productInfo);
        }
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
            var options = Options.Create(new ServiceManagerOptions());
            var connectionFactory = new ManagementConnectionFactory(options, nameProvider, NullLoggerFactory.Instance);
            var headers = connectionFactory.GetRequestHeaders();
            Assert.True(headers.TryGetValue(Constants.Headers.AsrsServerId, out var serverId));
            return serverId;
        }

        var serverId1 = GetServerId(nameProvider1);
        var serverId2 = GetServerId(nameProvider2);
        Assert.NotEqual(serverId1, serverId2);
    }
}
