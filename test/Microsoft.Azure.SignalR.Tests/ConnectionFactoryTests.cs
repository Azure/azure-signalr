// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace Microsoft.Azure.SignalR.Tests;

#nullable enable

public class ConnectionFactoryTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("foo", "bar")]
    public void TestSetCustomHeaders(string? key, string? val)
    {
        var nameProvider = new DefaultServerNameProvider();

        var loggerFactory = NullLoggerFactory.Instance;

        var options = Options.Create(new ServiceOptions());

        if (key != null && val != null)
        {
            options.Value.CustomHeaderProvider = headers =>
            {
                headers.Add(key, val);
            };
        }

        var factory = new ConnectionFactory(nameProvider, options, loggerFactory);

        var headers = factory.GetRequestHeaders();
        Assert.True(headers.TryGetValue(Constants.AsrsUserAgent, out var productInfo));
        Assert.StartsWith("Microsoft.Azure.SignalR.Common", productInfo);

        Assert.True(headers.TryGetValue(Constants.Headers.AsrsServerId, out var serverId));
        Assert.Equal(nameProvider.GetName(), serverId);

        if (key != null && val != null)
        {
            Assert.True(headers.TryGetValue(key, out var actualVal));
            Assert.Equal(val, actualVal);
        }
    }

    [Theory]
    [InlineData(Constants.AsrsUserAgent, "bar")]
    [InlineData(Constants.Headers.AsrsServerId, "bar")]
    [InlineData("asrs-x", "bar")]
    [InlineData("x-asrs-foo", "bar")]
    public void TestSetCustomHeadersThrows(string key, string val)
    {
        var nameProvider = new DefaultServerNameProvider();

        var loggerFactory = NullLoggerFactory.Instance;

        var options = Options.Create(new ServiceOptions());

        options.Value.CustomHeaderProvider = headers =>
        {
            headers.Add(key, val);
        };

        var factory = new ConnectionFactory(nameProvider, options, loggerFactory);

        var exception = Assert.Throws<ArgumentException>(factory.GetRequestHeaders);
        Assert.Contains(key, exception.Message!);
    }
}
