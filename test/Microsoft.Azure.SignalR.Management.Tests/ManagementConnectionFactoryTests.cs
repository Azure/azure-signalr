// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests;

#nullable enable

public class ManagementConnectionFactoryTests
{
    private const string OptionsAsrsUserAgent = $"Microsoft.Azure.SignalR.Management.123456";

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
            Assert.StartsWith("Microsoft.Azure.SignalR.Management", productInfo);
        }
        else
        {
            Assert.StartsWith("Microsoft.Azure.SignalR.Common", productInfo);
        }
    }
}
