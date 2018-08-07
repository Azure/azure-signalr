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
        public void ValidPreviewConnectionString(string expectedEndpoint, string connectionString)
        {
            (var endpoint, var accessKey, var version, var port) = ServiceEndpointUtility.ParseConnectionString(connectionString);

            Assert.Equal(expectedEndpoint, endpoint);
            Assert.Equal("bbb", accessKey);
            Assert.Null(version);
            Assert.Null(port);
        }

        [Theory]
        [InlineData("https://aaa", "1.0", null, "endpoint=https://aaa;AccessKey=bbb;version=1.0")]
        [InlineData("https://aaa", "1.0", null, "ENDPOINT=https://aaa/;ACCESSKEY=bbb;VERSION=1.0")]
        [InlineData("http://aaa", "1.1", null, "endpoint=http://aaa;AccessKey=bbb;Version=1.1")]
        [InlineData("http://aaa", "2.0", 1234, "ENDPOINT=http://aaa/;ACCESSKEY=bbb;Version=2.0;Port=1234")]
        public void ValidConnectionString(string expectedEndpoint, string expectedVersion, int? expectedPort, string connectionString)
        {
            (var endpoint, var accessKey, var version, var port) = ServiceEndpointUtility.ParseConnectionString(connectionString);

            Assert.Equal(expectedEndpoint, endpoint);
            Assert.Equal("bbb", accessKey);
            Assert.Equal(expectedVersion, version);
            Assert.Equal(expectedPort, port);
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
