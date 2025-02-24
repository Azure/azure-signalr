// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.ServiceConnections;

#nullable enable

public class ConnectionFactoryTests
{
    private const string MockAsrsUserAgent = "bar";

    [Theory]
    [InlineData(null, null)]
    [InlineData("foo", "bar")]
    public void TestGetRequestHeaders(string? key, string? val)
    {
        var nameProvider = new DefaultServerNameProvider();

        var loggerFactory = NullLoggerFactory.Instance;

        TestHeaderProvider? customHeaderProvider = null;
        if (key != null && val != null)
        {
            customHeaderProvider = new TestHeaderProvider();
            customHeaderProvider.SetHeader(key, val);
            customHeaderProvider.SetHeader(Constants.AsrsUserAgent, MockAsrsUserAgent);
        }

        var factory = new ConnectionFactory(nameProvider, customHeaderProvider, loggerFactory);

        var headers = factory.GetRequestHeaders();
        Assert.True(headers.TryGetValue(Constants.AsrsUserAgent, out var productInfo));
        Assert.StartsWith("Microsoft.Azure.SignalR.Common", productInfo);

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
