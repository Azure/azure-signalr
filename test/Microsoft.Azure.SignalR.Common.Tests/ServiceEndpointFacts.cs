// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests
{
    public class ServiceEndpointFacts
    {
        [Theory]
        [InlineData("Azure:SignalR:ConnectionString", "", EndpointType.Primary)]
        [InlineData("Azure:SignalR:ConnectionString:a", "a", EndpointType.Primary)]
        [InlineData("Azure:SignalR:ConnectionString:a:primary", "a", EndpointType.Primary)]
        [InlineData("Azure:SignalR:ConnectionString:a:Primary", "a", EndpointType.Primary)]
        [InlineData("Azure:SignalR:ConnectionString:secondary", "secondary", EndpointType.Primary)]
        [InlineData("Azure:SignalR:ConnectionString:a:secondary", "a", EndpointType.Secondary)]
        [InlineData("Azure:SignalR:ConnectionString::secondary", "", EndpointType.Secondary)]
        internal void TestParseKey(string key, string expectedName, EndpointType expectedType)
        {
            var (name, type) = ServiceEndpoint.ParseKey(key);

            Assert.Equal(expectedName, name);
            Assert.Equal(expectedType, type);
        }

        [Theory]
        [InlineData("a")]
        [InlineData("")]
        internal void TestParseInvalidKey(string key)
        {
            Assert.Throws<ArgumentException>(() => ServiceEndpoint.ParseKey(key));
        }
    }
}
