﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using Azure;
using Azure.Identity;

using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests
{
    public class ServiceEndpointFacts
    {
        private const string HttpEndpoint = "http://aaa";

        private const string HttpsEndpoint = "https://aaa";

        private const string HttpClientEndpoint = "http://bbb";

        private const string HttpsClientEndpoint = "http://bbb";

        private const string DefaultKey = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        [Theory]
        [ClassData(typeof(EndpointAndPortTestData))]
        public void TestEndpointAndAudience(string connectionString, string expectedAudience, string expectedEndpoint)
        {
            var endpoint = new ServiceEndpoint(connectionString);
            Assert.Equal(expectedAudience, endpoint.AudienceBaseUrl);
            Assert.Equal(expectedEndpoint, endpoint.Endpoint);
            Assert.Equal(expectedEndpoint, endpoint.Endpoint);
            Assert.Equal(expectedEndpoint, endpoint.ClientEndpoint);
        }

        [Theory]
        [ClassData(typeof(ClientEndpointTestData))]
        public void TestClientEndpoint(string connectionString, string expectedClientEndpoint)
        {
            var endpoint = new ServiceEndpoint(connectionString);
            Assert.Equal(expectedClientEndpoint, endpoint.ClientEndpoint);
        }

        [Theory]
        [ClassData(typeof(EndpointEndWithSlash))]
        public void TestEndpointEndWithSlash(string connectionString, string expectedEndpoint)
        {
            var endpoint = new ServiceEndpoint(connectionString);
            Assert.Equal(expectedEndpoint, endpoint.Endpoint);
        }

        [Theory]
        [InlineData("http://localhost", "http://localhost", 80)]
        [InlineData("https://localhost", "https://localhost", 443)]
        [InlineData("http://localhost:5050", "http://localhost:5050", 5050)]
        [InlineData("https://localhost:5050", "https://localhost:5050", 5050)]
        [InlineData("http://localhost/", "http://localhost", 80)]
        [InlineData("http://localhost/foo", "http://localhost", 80)]
        [InlineData("https://localhost/foo/", "https://localhost", 443)]
        public void TestTokenCredentialConstructor(string url, string expectedEndpoint, int port)
        {
            var uri = new Uri(url);
            var serviceEndpoint = new ServiceEndpoint(uri, new DefaultAzureCredential());
            var accessKey = Assert.IsType<AadAccessKey>(serviceEndpoint.AccessKey);
            Assert.False(accessKey.Authorized);
            Assert.Equal(expectedEndpoint, serviceEndpoint.Endpoint);
            Assert.Equal("", serviceEndpoint.Name);
            Assert.Equal(port, serviceEndpoint.AccessKey.Endpoint.Port);
            Assert.Equal(EndpointType.Primary, serviceEndpoint.EndpointType);
            TestCopyConstructor(serviceEndpoint);
        }

        [Theory]
        [InlineData("http://localhost", "http://localhost", 80)]
        [InlineData("https://localhost", "https://localhost", 443)]
        [InlineData("http://localhost:5050", "http://localhost:5050", 5050)]
        [InlineData("https://localhost:5050", "https://localhost:5050", 5050)]
        [InlineData("http://localhost/", "http://localhost", 80)]
        [InlineData("http://localhost/foo", "http://localhost", 80)]
        [InlineData("https://localhost/foo/", "https://localhost", 443)]
        public void TestAzureKeyCredentialConstructor(string url, string expectedEndpoint, int port)
        {
            var uri = new Uri(url);
            var serviceEndpoint = new ServiceEndpoint(uri, new AzureKeyCredential(DefaultKey));
            var accessKey = Assert.IsType<AccessKey>(serviceEndpoint.AccessKey);
            Assert.Equal(DefaultKey.GetHashCode().ToString(), accessKey.Id);
            Assert.Equal(DefaultKey, accessKey.Value);
            Assert.Equal(expectedEndpoint, serviceEndpoint.Endpoint);
            Assert.Equal("", serviceEndpoint.Name);
            Assert.Equal(port, serviceEndpoint.AccessKey.Endpoint.Port);
            Assert.Equal(EndpointType.Primary, serviceEndpoint.EndpointType);
            TestCopyConstructor(serviceEndpoint);
        }

        [Theory]
        [InlineData("ftp://localhost")]
        [InlineData("ws://localhost")]
        [InlineData("localhost:5050")]
        public void TestInvalidEndpoint(string url)
        {
            var uri = new Uri(url);
            Assert.Throws<ArgumentException>(() => new ServiceEndpoint(uri, new DefaultAzureCredential()));
            Assert.Throws<ArgumentException>(() => new ServiceEndpoint(uri, new AzureKeyCredential("foo")));
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
        public void TestTokenCredentialConstructorWithKey(string key, string name, EndpointType type)
        {
            var uri = new Uri("http://localhost");
            var serviceEndpoint = new ServiceEndpoint(key, uri, new DefaultAzureCredential());
            var accessKey = Assert.IsType<AadAccessKey>(serviceEndpoint.AccessKey);
            Assert.False(accessKey.Authorized);
            Assert.Equal(name, serviceEndpoint.Name);
            Assert.Equal(type, serviceEndpoint.EndpointType);
            TestCopyConstructor(serviceEndpoint);
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
        public void TestAzureKeyCredentialConstructorWithKey(string key, string name, EndpointType type)
        {
            var uri = new Uri("http://localhost");
            var serviceEndpoint = new ServiceEndpoint(key, uri, new AzureKeyCredential(DefaultKey));
            var accessKey = Assert.IsType<AccessKey>(serviceEndpoint.AccessKey);
            Assert.Equal(DefaultKey.GetHashCode().ToString(), accessKey.Id);
            Assert.Equal(DefaultKey, accessKey.Value);
            Assert.Equal(name, serviceEndpoint.Name);
            Assert.Equal(type, serviceEndpoint.EndpointType);
            TestCopyConstructor(serviceEndpoint);
        }

        [Theory]
        [ClassData(typeof(EndpointEqualityTestData))]
        public void TestEndpointsEquality(ServiceEndpoint first, ServiceEndpoint second, bool expected)
        {
            Assert.Equal(expected, first.Equals(second));
            Assert.Equal(expected, first.GetHashCode() == second.GetHashCode());
        }

        private static void TestCopyConstructor(ServiceEndpoint endpoint)
        {
            var other = new ServiceEndpoint(endpoint);
            Assert.Equal(endpoint.Name, other.Name);
            Assert.Equal(endpoint.EndpointType, other.EndpointType);
            Assert.Equal(endpoint.Endpoint, other.Endpoint);
            Assert.Equal(endpoint.ClientEndpoint, other.ClientEndpoint);
            Assert.Equal(endpoint.Version, other.Version);
            Assert.Equal(endpoint.AccessKey, other.AccessKey);
        }

        public class EndpointEqualityTestData : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[]
                {
                    new ServiceEndpoint("a", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                    new ServiceEndpoint("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                    false,
                };
                yield return new object[]
                {
                    new ServiceEndpoint("a", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                    new ServiceEndpoint(":primary", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080"),
                    false,
                };
                yield return new object[]
                {
                    new ServiceEndpoint("a", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                    new ServiceEndpoint("a:secondary", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"),
                    false,
                };
                yield return new object[]
                {
                    new ServiceEndpoint("a", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                    new ServiceEndpoint("b", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456780"),
                    false,
                };
                yield return new object[]
                {
                    new ServiceEndpoint("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                    new ServiceEndpoint(":secondary", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456780"),
                    false,
                };
                yield return new object[]
                {
                    new ServiceEndpoint("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0", EndpointType.Secondary),
                    new ServiceEndpoint("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456780", name: "Name1"),
                    false,
                };
                yield return new object[]
                {
                    new ServiceEndpoint("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                    new ServiceEndpoint("Endpoint=https://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456780"),
                    false,
                };
                yield return new object[]
                {
                    new ServiceEndpoint("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                    new ServiceEndpoint("Endpoint=http://localhost;AccessKey=OPQRSTUVWXYZ0123456780ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456780"),
                    false, // ports are different
                };
                yield return new object[]
                {
                    new ServiceEndpoint("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                    new ServiceEndpoint(":primary", "Endpoint=http://localhost;AccessKey=OPQRSTUVWXYZ0123456780ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456780"),
                    false, // ports are different 
                };
                yield return new object[]
                {
                    new ServiceEndpoint(":secondary", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                    new ServiceEndpoint("Endpoint=http://localhost;AccessKey=OPQRSTUVWXYZ0123456780ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456780", EndpointType.Secondary),
                    false, // ports are different
                };
                yield return new object[]
                {
                    new ServiceEndpoint("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0"),
                    new ServiceEndpoint("Endpoint=http://localhost:8080;AccessKey=OPQRSTUVWXYZ0123456780ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456780"),
                    true,
                };
                yield return new object[]
                {
                    new ServiceEndpoint("foo:bar", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Version=1.0"),
                    new ServiceEndpoint("Endpoint=http://localhost;AccessKey=OPQRSTUVWXYZ0123456780ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456780", name : "foo:bar"),
                    true,
                };
                yield return new object[]
                {
                    new ServiceEndpoint(":primary", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Version=1.0"),
                    new ServiceEndpoint("Endpoint=http://localhost;AccessKey=OPQRSTUVWXYZ0123456780ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456780"),
                    true,
                };
                yield return new object[]
                {
                    new ServiceEndpoint(":secondary", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Version=1.0"),
                    new ServiceEndpoint("Endpoint=http://localhost;AccessKey=OPQRSTUVWXYZ0123456780ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456780", EndpointType.Secondary),
                    true,
                };
                yield return new object[]
                {
                    new ServiceEndpoint("foo:secondary", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Version=1.0"),
                    new ServiceEndpoint("Endpoint=http://localhost;AccessKey=OPQRSTUVWXYZ0123456780ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456780", EndpointType.Secondary, "foo"),
                    true,
                };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public class EndpointEndWithSlash: IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { $"endpoint={HttpEndpoint};accesskey={DefaultKey}", HttpEndpoint};
                yield return new object[] { $"endpoint={HttpEndpoint}/;accesskey={DefaultKey}", HttpEndpoint};
                yield return new object[] { $"endpoint={HttpsEndpoint};accesskey={DefaultKey}", HttpsEndpoint};
                yield return new object[] { $"endpoint={HttpsEndpoint}/;accesskey={DefaultKey}", HttpsEndpoint};
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public class EndpointAndPortTestData : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                // http
                yield return new object[] { $"endpoint={HttpEndpoint};accesskey={DefaultKey}", HttpEndpoint, HttpEndpoint};
                yield return new object[] { $"endpoint={HttpEndpoint}:80;accesskey={DefaultKey}", HttpEndpoint, HttpEndpoint};
                yield return new object[] { $"endpoint={HttpEndpoint}:500;accesskey={DefaultKey}", HttpEndpoint, HttpEndpoint + ":500"};
                // https
                yield return new object[] { $"endpoint={HttpsEndpoint};accesskey={DefaultKey}", HttpsEndpoint, HttpsEndpoint};
                yield return new object[] { $"endpoint={HttpsEndpoint}:443;accesskey={DefaultKey}", HttpsEndpoint, HttpsEndpoint};
                yield return new object[] { $"endpoint={HttpsEndpoint}:500;accesskey={DefaultKey}", HttpsEndpoint, HttpsEndpoint + ":500"};
                // uppercase endpoint
                yield return new object[] { $"endpoint={HttpEndpoint.ToUpper()};accesskey={DefaultKey}", HttpEndpoint, HttpEndpoint};
                yield return new object[] { $"endpoint={HttpsEndpoint.ToUpper()};accesskey={DefaultKey}", HttpsEndpoint, HttpsEndpoint};
                // port override
                yield return new object[] { $"endpoint={HttpsEndpoint};accesskey={DefaultKey};port=500", HttpsEndpoint, HttpsEndpoint + ":500"};
                yield return new object[] { $"endpoint={HttpsEndpoint}:500;accesskey={DefaultKey};port=443", HttpsEndpoint, HttpsEndpoint};
                // uppercase property name
                yield return new object[] { $"ENDPOINT={HttpEndpoint};ACCESSKEY={DefaultKey}", HttpEndpoint, HttpEndpoint};
                yield return new object[] { $"ENDPOINT={HttpsEndpoint}:500;ACCESSKEY={DefaultKey};PORT=443", HttpsEndpoint, HttpsEndpoint};
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public class ClientEndpointTestData : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { $"endpoint={HttpEndpoint};authType=aad;clientEndpoint={HttpClientEndpoint}", HttpClientEndpoint };
                yield return new object[] { $"endpoint={HttpEndpoint};authType=aad;clientEndpoint={HttpClientEndpoint}:80", HttpClientEndpoint + ":80" };
                yield return new object[] { $"endpoint={HttpEndpoint};authType=aad;clientEndpoint={HttpsClientEndpoint}", HttpsClientEndpoint };
                yield return new object[] { $"endpoint={HttpEndpoint};authType=aad;clientEndpoint={HttpsClientEndpoint}:443", HttpsClientEndpoint + ":443" };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}