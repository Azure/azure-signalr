// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Azure.Identity;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests
{
    public class ConnectionStringParserFacts
    {
        [Theory]
        [InlineData("https://aaa", "endpoint=https://aaa;AccessKey=bbb;")]
        [InlineData("https://aaa", "ENDPOINT=https://aaa/;ACCESSKEY=bbb;")]
        [InlineData("http://aaa", "endpoint=http://aaa;AccessKey=bbb;")]
        [InlineData("http://aaa", "ENDPOINT=http://aaa/;ACCESSKEY=bbb;")]
        public void ValidPreviewConnectionString(string expectedEndpoint, string connectionString)
        {
            var (accessKey, version, clientEndpoint) = ConnectionStringParser.Parse(connectionString);

            Assert.Equal(expectedEndpoint, accessKey.Endpoint);
            Assert.Equal("bbb", accessKey.Value);
            Assert.Null(version);
            Assert.Null(accessKey.Port);
        }

        [Theory]
        [InlineData("endpoint=https://aaa;AuthType=aad;clientId=123;tenantId=aaaaaaaa-bbbb-bbbb-bbbb-cccccccccccc")]
        public void InvliadApplicationConnectionString(string connectionString)
        {
            Assert.Throws<ArgumentException>(() => ConnectionStringParser.Parse(connectionString));
        }

        [Theory]
        [InlineData("https://aaa", "endpoint=https://aaa;AuthType=aad;")]
        // simply ignore the clientSecret
        [InlineData("https://aaa", "endpoint=https://aaa;AuthType=aad;clientSecret=xxxx;")]
        // simply ignore the tenantId
        [InlineData("https://aaa", "endpoint=https://aaa;AuthType=aad;tenantId=xxxx;")]
        [InlineData("https://aaa", "endpoint=https://aaa;AuthType=aad;clientId=123;")]
        internal void ValidMSIConnectionString(string expectedEndpoint, string connectionString)
        {
            var (accessKey, version, clientEndpoint) = ConnectionStringParser.Parse(connectionString);

            Assert.Equal(expectedEndpoint, accessKey.Endpoint);
            Assert.IsType<AadAccessKey>(accessKey);
            if (accessKey is AadAccessKey aadAccessKey)
            {
                Assert.IsType<ManagedIdentityCredential>(aadAccessKey.TokenCredential);
            }
            Assert.Null(version);
            Assert.Null(accessKey.Port);
            Assert.Null(clientEndpoint);
        }

        [Theory]
        [InlineData("https://aaa", "endpoint=https://aaa;AuthType=aad;clientId=foo;clientSecret=bar;tenantId=aaaaaaaa-bbbb-bbbb-bbbb-cccccccccccc")]
        public void ValidApplicationConnectionString(string expectedEndpoint, string connectionString)
        {
            var (accessKey, version, clientEndpoint) = ConnectionStringParser.Parse(connectionString);

            Assert.Equal(expectedEndpoint, accessKey.Endpoint);
            Assert.IsType<AadAccessKey>(accessKey);
            if (accessKey is AadAccessKey aadAccessKey)
            {
                Assert.IsType<ClientSecretCredential>(aadAccessKey.TokenCredential);
            }
            Assert.Null(version);
            Assert.Null(accessKey.Port);
            Assert.Null(clientEndpoint);
        }

        [Theory]
        [InlineData("https://aaa", "1.0", null, "endpoint=https://aaa;AccessKey=bbb;version=1.0")]
        [InlineData("https://aaa", "1.0-preview", null, "ENDPOINT=https://aaa/;ACCESSKEY=bbb;VERSION=1.0-preview")]
        [InlineData("http://aaa", "1.1", null, "endpoint=http://aaa;AccessKey=bbb;Version=1.1")]
        [InlineData("http://aaa", "1.1-beta2", 1234, "ENDPOINT=http://aaa/;ACCESSKEY=bbb;Version=1.1-beta2;Port=1234")]
        public void ValidConnectionString(string expectedEndpoint, string expectedVersion, int? expectedPort, string connectionString)
        {
            var (accessKey, version, clientEndpoint) = ConnectionStringParser.Parse(connectionString);

            Assert.Equal(expectedEndpoint, accessKey.Endpoint);
            Assert.Equal("bbb", accessKey.Value);
            Assert.Equal(expectedVersion, version);
            Assert.Equal(expectedPort, accessKey.Port);
            Assert.Null(clientEndpoint);
        }

        [Theory]
        [InlineData("Endpoint=xxx")]
        [InlineData("AccessKey=xxx")]
        [InlineData("XXX=yyy")]
        [InlineData("XXX")]
        public void InvalidConnectionStrings(string connectionString)
        {
            var exception = Assert.Throws<ArgumentException>(() => ConnectionStringParser.Parse(connectionString));

            Assert.Contains("Connection string missing required properties", exception.Message);
        }

        [Theory]
        [InlineData("Endpoint=aaa;AccessKey=bbb;")]
        [InlineData("Endpoint=endpoint=aaa;AccessKey=bbb;")]
        public void InvalidEndpoint(string connectionString)
        {
            var exception = Assert.Throws<ArgumentException>(() => ConnectionStringParser.Parse(connectionString));

            Assert.Contains("Endpoint property in connection string is not a valid URI", exception.Message);
        }

        [Theory]
        [InlineData("endpoint=https://aaa;clientEndpoint=aaa;AccessKey=bbb;")]
        [InlineData("endpoint=https://aaa;ClientEndpoint=endpoint=aaa;AccessKey=bbb;")]
        public void InvalidClientEndpoint(string connectionString)
        {
            var exception = Assert.Throws<ArgumentException>(() => ConnectionStringParser.Parse(connectionString));

            Assert.Contains("ClientEndpoint property in connection string is not a valid URI", exception.Message);
        }

        [Theory]
        [InlineData("Endpoint=https://aaa;AccessKey=bbb;version=abc", "abc")]
        [InlineData("Endpoint=https://aaa;AccessKey=bbb;version=1.x", "1.x")]
        [InlineData("Endpoint=https://aaa;AccessKey=bbb;version=2.0", "2.0")]
        public void InvalidVersion(string connectionString, string version)
        {
            var exception = Assert.Throws<ArgumentException>(() => ConnectionStringParser.Parse(connectionString));

            Assert.Contains(string.Format("Version {0} is not supported.", version), exception.Message);
        }

        [Theory]
        [InlineData("Endpoint=https://aaa;AccessKey=bbb;version=1.0;port=2.3")]
        [InlineData("Endpoint=https://aaa;AccessKey=bbb;version=1.1;port=1000000")]
        [InlineData("Endpoint=https://aaa;AccessKey=bbb;version=1.0-preview;port=0")]
        public void InvalidPort(string connectionString)
        {
            var exception = Assert.Throws<ArgumentException>(() => ConnectionStringParser.Parse(connectionString));

            Assert.Contains(@"Invalid value for port property.", exception.Message);
        }
    }
}
