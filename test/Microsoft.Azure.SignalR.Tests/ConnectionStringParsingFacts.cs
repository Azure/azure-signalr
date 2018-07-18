// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class ConnectionStringParsingFacts
    {
        [Theory]
        [InlineData("https://aaa", "endpoint=https://aaa;AccessKey=bbb;")]
        [InlineData("https://aaa", "ENDPOINT=https://aaa/;ACCESSKEY=bbb;")]
        [InlineData("http://aaa", "endpoint=http://aaa;AccessKey=bbb;")]
        [InlineData("http://aaa", "ENDPOINT=http://aaa/;ACCESSKEY=bbb;")]
        public void ValidConnectionString(string expectedEndpoint, string connectionString)
        {
            (var endpoint, var accessKey) = ServiceEndpointUtility.ParseConnectionString(connectionString);

            Assert.Equal(expectedEndpoint, endpoint);
            Assert.Equal("bbb", accessKey);
        }

        [Theory]
        [InlineData("Endpoint=xxx")]
        [InlineData("AccessKey=xxx")]
        [InlineData("XXX=yyy")]
        [InlineData("XXX")]
        public void InvalidConnectionStrings(string connectionString)
        {
            var exception = Assert.Throws<ArgumentException>(() => 
                ServiceEndpointUtility.ParseConnectionString(connectionString));

            Assert.Contains("Connection string missing required properties", exception.Message);
        }

        [Theory]
        [InlineData("Endpoint=aaa;AccessKey=bbb;")]
        [InlineData("Endpoint=endpoint=aaa;AccessKey=bbb;")]
        public void InvalidEndpoint(string connectionString)
        {
            var exception = Assert.Throws<ArgumentException>(() =>
                ServiceEndpointUtility.ParseConnectionString(connectionString));

            Assert.Contains("Endpoint property in connection string is not a valid URI", exception.Message);
        }
    }
}
