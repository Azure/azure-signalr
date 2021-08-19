// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Azure.Identity;

using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests
{
    public class ServiceEndpointFacts
    {
        public static IEnumerable<object[]> TestEndpointsEqualityInput = new object[][]
        {
            new object[]
            {
                new ServiceEndpoint("a", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                new ServiceEndpoint("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                false,
            },
            new object[]
            {
                new ServiceEndpoint("a", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                new ServiceEndpoint(":primary", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080"),
                false,
            },
            new object[]
            {
                new ServiceEndpoint("a", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                new ServiceEndpoint("a:secondary", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"),
                false,
            },
            new object[]
            {
                new ServiceEndpoint("a", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                new ServiceEndpoint("b", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456780"),
                false,
            },
            new object[]
            {
                new ServiceEndpoint("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                new ServiceEndpoint(":secondary", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456780"),
                false,
            },
            new object[]
            {
                new ServiceEndpoint("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0", EndpointType.Secondary),
                new ServiceEndpoint("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456780", name: "Name1"),
                false,
            },
            new object[]
            {
                new ServiceEndpoint("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                new ServiceEndpoint("Endpoint=https://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456780"),
                false,
            },
            new object[]
            {
                new ServiceEndpoint("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                new ServiceEndpoint("Endpoint=http://localhost;AccessKey=OPQRSTUVWXYZ0123456780ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456780"),
                true,
            },
            new object[]
            {
                new ServiceEndpoint("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                new ServiceEndpoint(":primary", "Endpoint=http://localhost;AccessKey=OPQRSTUVWXYZ0123456780ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456780"),
                true,
            },
            new object[]
            {
                new ServiceEndpoint(":secondary", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                new ServiceEndpoint("Endpoint=http://localhost;AccessKey=OPQRSTUVWXYZ0123456780ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456780", EndpointType.Secondary),
                true,
            }
        };

        [Theory]
        [InlineData("http://localhost", "http://localhost", 80)]
        [InlineData("http://localhost:80", "http://localhost", 80)]
        [InlineData("https://localhost", "https://localhost", 443)]
        [InlineData("https://localhost:443", "https://localhost", 443)]
        [InlineData("http://localhost:5050", "http://localhost", 5050)]
        [InlineData("https://localhost:5050", "https://localhost", 5050)]
        [InlineData("http://localhost/", "http://localhost", 80)]
        [InlineData("http://localhost/foo", "http://localhost", 80)]
        [InlineData("https://localhost/foo/", "https://localhost", 443)]
        public void TestAadConstructor(string url, string endpoint, int? port)
        {
            var uri = new Uri(url);
            var serviceEndpoint = new ServiceEndpoint(uri, new DefaultAzureCredential());
            Assert.IsType<AadAccessKey>(serviceEndpoint.AccessKey);
            Assert.Equal(endpoint, serviceEndpoint.Endpoint);
            Assert.Equal(port, serviceEndpoint.Port);
            Assert.Equal("", serviceEndpoint.Name);
            Assert.Equal(EndpointType.Primary, serviceEndpoint.EndpointType);
            TestCopyConstructor(serviceEndpoint);
        }

        [Theory]
        [InlineData("ftp://localhost")]
        [InlineData("ws://localhost")]
        [InlineData("localhost:5050")]
        public void TestAadConstructorThrowsError(string url)
        {
            var uri = new Uri(url);
            Assert.Throws<ArgumentException>(() => new ServiceEndpoint(uri, new DefaultAzureCredential()));
        }

        [Theory]
        [InlineData("", "", EndpointType.Primary)]
        [InlineData("foo", "foo", EndpointType.Primary)]
        [InlineData("foo:primary", "foo", EndpointType.Primary)]
        [InlineData("foo:secondary", "foo", EndpointType.Secondary)]
        [InlineData("foo:SECONDARY", "foo", EndpointType.Secondary)]
        [InlineData("foo:bar", "foo:bar", EndpointType.Primary)]
        [InlineData(":", ":", EndpointType.Primary)]
        [InlineData(":bar", ":bar", EndpointType.Primary)]
        [InlineData(":primary", "", EndpointType.Primary)]
        [InlineData(":secondary", "", EndpointType.Secondary)]
        public void TestAadConstructorWithKey(string key, string name, EndpointType type)
        {
            var uri = new Uri("http://localhost");
            var serviceEndpoint = new ServiceEndpoint(key, uri, new DefaultAzureCredential());
            Assert.IsType<AadAccessKey>(serviceEndpoint.AccessKey);
            Assert.Equal(name, serviceEndpoint.Name);
            Assert.Equal(type, serviceEndpoint.EndpointType);
            TestCopyConstructor(serviceEndpoint);
        }

        [Theory]
        [MemberData(nameof(TestEndpointsEqualityInput))]
        public void TestEndpointsEquality(ServiceEndpoint first, ServiceEndpoint second, bool equal)
        {
            Assert.Equal(first.Equals(second), equal);
            Assert.Equal(first.GetHashCode() == second.GetHashCode(), equal);
        }

        private static void TestCopyConstructor(ServiceEndpoint endpoint)
        {
            var other = new ServiceEndpoint(endpoint);
            Assert.Equal(endpoint.Name, other.Name);
            Assert.Equal(endpoint.EndpointType, other.EndpointType);
            Assert.Equal(endpoint.Endpoint, other.Endpoint);
            Assert.Equal(endpoint.Port, other.Port);
            Assert.Equal(endpoint.ClientEndpoint, other.ClientEndpoint);
            Assert.Equal(endpoint.Version, other.Version);
            Assert.Equal(endpoint.AccessKey, other.AccessKey);
        }
    }
}