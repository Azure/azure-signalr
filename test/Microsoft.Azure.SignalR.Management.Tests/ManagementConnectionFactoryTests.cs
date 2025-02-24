// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests;

#nullable enable

public class ManagementConnectionFactoryTests
{
    private const string CustomAsrsUserAgent = "bar";

    private const string OptionsAsrsUserAgent = $"Microsoft.Azure.SignalR.Management.123456";

    [Theory]
    [InlineData(null, null, false)]
    [InlineData(null, null, true)]
    [InlineData("foo", "bar", false)]
    [InlineData("foo", "bar", true)]
    public void TestGetRequestHeaders(string? key, string? val, bool setManagementProductInfo)
    {
        var nameProvider = new DefaultServerNameProvider();

        var loggerFactory = NullLoggerFactory.Instance;

        TestHeaderProvider? customHeaderProvider = null;
        if (key != null && val != null)
        {
            customHeaderProvider = new TestHeaderProvider();
            customHeaderProvider.SetHeader(key, val);
            customHeaderProvider.SetHeader(Constants.AsrsUserAgent, CustomAsrsUserAgent);
        }

        var options = Options.Create(new ServiceManagerOptions());
        if (setManagementProductInfo)
        {
            options.Value.ProductInfo = OptionsAsrsUserAgent;
        }

        var factory = new ManagementConnectionFactory(options, nameProvider, customHeaderProvider, loggerFactory);

        var headers = factory.GetRequestHeaders();
        Assert.True(headers.TryGetValue(Constants.AsrsUserAgent, out var productInfo));

        if (setManagementProductInfo)
        {
            Assert.StartsWith("Microsoft.Azure.SignalR.Management", productInfo);
        }
        else
        {
            Assert.StartsWith("Microsoft.Azure.SignalR.Common", productInfo);
        }

        if (key != null && val != null)
        {
            Assert.True(headers.TryGetValue(key, out var actualVal));
            Assert.Equal(val, actualVal);
        }
    }

    private sealed class TestHeaderProvider : ICustomHeaderProvider
    {
        private readonly Dictionary<string, string> _headers = [];

        public void SetHeader(string key, string value)
        {
            _headers[key] = value;
        }

        public IDictionary<string, string> GetCustomHeaders()
        {
            return _headers;
        }
    }
}
