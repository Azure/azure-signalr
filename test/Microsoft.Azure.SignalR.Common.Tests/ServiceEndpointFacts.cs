// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using Azure.Identity;
using Xunit;

#nullable enable

namespace Microsoft.Azure.SignalR.Common.Tests;

public class ServiceEndpointFacts
{
    private const string HttpEndpoint = "http://aaa";

    private const string HttpsEndpoint = "https://aaa";

    private const string HttpClientEndpoint = "http://bbb";

    private const string HttpsClientEndpoint = "http://bbb";

    private const string DefaultKey = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private const string DefaultServiceEndpoint = "https://test.service.signalr.net";

    public static IEnumerable<object[]> ServerEndpointTestData
    {
        get
        {
            yield return new object[] { $"endpoint={HttpEndpoint};authType=aad;serverEndpoint={HttpClientEndpoint}", HttpClientEndpoint };
            yield return new object[] { $"endpoint={HttpEndpoint};authType=aad;serverEndpoint={HttpClientEndpoint}:80", HttpClientEndpoint + ":80" };
            yield return new object[] { $"endpoint={HttpEndpoint};authType=aad;serverEndpoint={HttpClientEndpoint}:80/abc", HttpClientEndpoint + ":80/abc" };
            yield return new object[] { $"endpoint={HttpsEndpoint};authType=aad;serverEndpoint={HttpsClientEndpoint}", HttpsClientEndpoint };
            yield return new object[] { $"endpoint={HttpsEndpoint};authType=aad;serverEndpoint={HttpsClientEndpoint}:443", HttpsClientEndpoint + ":443" };
        }
    }

    private static Uri DefaultServiceEndpointUri { get; } = new Uri(DefaultServiceEndpoint);

    [Theory]
    [ClassData(typeof(EndpointAndPortTestData))]
    public void TestEndpointAndAudience(string connectionString, string expectedAudience, string expectedEndpoint)
    {
        var endpoint = new ServiceEndpoint(connectionString);
        Assert.Equal(expectedAudience, endpoint.AudienceBaseUrl);
        Assert.Equal(expectedEndpoint, endpoint.Endpoint);
        Assert.Equal(new Uri(expectedEndpoint), endpoint.ClientEndpoint);
        Assert.Equal(new Uri(expectedEndpoint), endpoint.ServerEndpoint);
    }

    [Theory]
    [ClassData(typeof(ClientEndpointTestData))]
    public void TestClientEndpoint(string connectionString, string expectedClientEndpoint)
    {
        var endpoint = new ServiceEndpoint(connectionString);
        Assert.Equal(new Uri(expectedClientEndpoint), endpoint.ClientEndpoint);
    }

    [Theory]
    [MemberData(nameof(ServerEndpointTestData))]
    public void TestServerEndpoint(string connectionString, string expectedServerEndpoint)
    {
        var endpoint = new ServiceEndpoint(connectionString);
        Assert.Equal(new Uri(expectedServerEndpoint), endpoint.ServerEndpoint);
    }

    [Theory]
    [ClassData(typeof(EndpointEndWithSlash))]
    public void TestEndpointEndWithSlash(string connectionString, string expectedEndpoint)
    {
        var endpoint = new ServiceEndpoint(connectionString);
        Assert.Equal(new Uri(expectedEndpoint), new Uri(endpoint.Endpoint));
    }

    [Fact]
    public void TestCustomizeEndpointInConstructor()
    {
        var clientEndpoint = new Uri("https://clientEndpoint/path");
        var serverEndpoint = new Uri("http://serverEndpoint:123/path");
        var endpoint = "https://test.service.signalr.net";
        var serviceEndpoints = new ServiceEndpoint[]{
            new ServiceEndpoint(new Uri(endpoint), new DefaultAzureCredential())
            {
                ClientEndpoint = clientEndpoint,
                ServerEndpoint = serverEndpoint
            },
            new ServiceEndpoint($"Endpoint={endpoint};AccessKey={DefaultKey}")
            {
                ClientEndpoint = clientEndpoint,
                ServerEndpoint = serverEndpoint
            }
        };
        foreach (var serviceEndpoint in serviceEndpoints)
        {
            Assert.Equal(endpoint, serviceEndpoint.Endpoint);
            Assert.Equal(clientEndpoint, serviceEndpoint.ClientEndpoint);
            Assert.Equal(serverEndpoint, serviceEndpoint.ServerEndpoint);
        }
    }

    [Fact]
    public void TestCreateServiceEndpointFromAnother()
    {
        var serviceEndpoint = new ServiceEndpoint($"Endpoint=http://abc.service.signalr.net;AccessKey={DefaultKey}", EndpointType.Secondary, "name1")
        {
            ClientEndpoint = new Uri("https://clientEndpoint/path"),
            ServerEndpoint = new Uri("http://serverEndpoint:123/path")
        };
        var cloned = new ServiceEndpoint(serviceEndpoint);
        Assert.Equal(serviceEndpoint.Endpoint, cloned.Endpoint);
        Assert.Equal(serviceEndpoint.ServerEndpoint, cloned.ServerEndpoint);
        Assert.Equal(serviceEndpoint.ClientEndpoint, cloned.ClientEndpoint);
        Assert.Equal(serviceEndpoint.EndpointType, cloned.EndpointType);
        Assert.Equal(serviceEndpoint.Name, cloned.Name);
        Assert.Equal(serviceEndpoint.ConnectionString, cloned.ConnectionString);
        Assert.Equal(serviceEndpoint.AudienceBaseUrl, cloned.AudienceBaseUrl);
        Assert.Equal(serviceEndpoint.AccessKey, cloned.AccessKey);
    }

    [Theory]
    [InlineData("http://localhost", "http://localhost", 80)]
    [InlineData("https://localhost", "https://localhost", 443)]
    [InlineData("http://localhost:5050", "http://localhost:5050", 5050)]
    [InlineData("https://localhost:5050", "https://localhost:5050", 5050)]
    [InlineData("http://localhost/", "http://localhost", 80)]
    [InlineData("http://localhost/foo", "http://localhost", 80)]
    [InlineData("https://localhost/foo/", "https://localhost", 443)]
    public void TestAzureCredentialConstructor(string url, string expectedEndpoint, int port)
    {
        var uri = new Uri(url);
        var endpoint = new ServiceEndpoint(uri, new DefaultAzureCredential());
        Assert.Equal(port, endpoint.ClientEndpoint.Port);
        Assert.Equal(port, endpoint.ServerEndpoint.Port);

        Assert.IsType<MicrosoftEntraAccessKey>(endpoint.AccessKey);
        Assert.Equal(expectedEndpoint, endpoint.Endpoint);
        Assert.Equal("", endpoint.Name);
        Assert.Equal(EndpointType.Primary, endpoint.EndpointType);
        TestCopyConstructor(endpoint);
    }

    [Theory]
    [InlineData("ftp://localhost")]
    [InlineData("ws://localhost")]
    [InlineData("localhost:5050")]
    public void TestAzureCredentialConstructorThrowsError(string url)
    {
        var uri = new Uri(url);
        var e = Assert.Throws<ArgumentException>(() => new ServiceEndpoint(uri, new DefaultAzureCredential()));
        Assert.Equal("Endpoint scheme must be 'http://' or 'https://'", e.Message);
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
    public void TestAzureCredentialConstructorWithKey(string key, string name, EndpointType type)
    {
        var uri = new Uri("http://localhost");
        var serviceEndpoint = new ServiceEndpoint(key, uri, new DefaultAzureCredential());
        Assert.IsType<MicrosoftEntraAccessKey>(serviceEndpoint.AccessKey);
        Assert.Equal(name, serviceEndpoint.Name);
        Assert.Equal(type, serviceEndpoint.EndpointType);
        TestCopyConstructor(serviceEndpoint);
    }

    [Fact]
    public void TestAzureCredentialConstructorWithServerEndpoint()
    {
        var credential = new DefaultAzureCredential();

        // use default endpoint
        var endpoint = new ServiceEndpoint(new Uri(DefaultServiceEndpoint), credential);
        Assert.Equal(ServiceEndpoint.BuildEndpointString(DefaultServiceEndpointUri), endpoint.Endpoint);
        var key = Assert.IsType<MicrosoftEntraAccessKey>(endpoint.AccessKey);
        Assert.Equal(DefaultServiceEndpointUri, endpoint.ServerEndpoint);
        Assert.Equal($"{DefaultServiceEndpoint}/api/v1/auth/accessKey", key.GetAccessKeyUrl, StringComparer.OrdinalIgnoreCase);

        // throw if server endpoint is invalid
        var invalidUri = new Uri("ftp://foo");
        Assert.Throws<ArgumentException>(() => new ServiceEndpoint(invalidUri, credential));
        Assert.Throws<ArgumentException>(() => new ServiceEndpoint(new Uri(DefaultServiceEndpoint), credential, clientEndpoint: invalidUri));
        Assert.Throws<ArgumentException>(() => new ServiceEndpoint(new Uri(DefaultServiceEndpoint), credential)
        {
            ClientEndpoint = invalidUri,
        });

        // use constructor param
        var serverEndpoint1 = new Uri("http://serverEndpoint:123");
        endpoint = new ServiceEndpoint(new Uri(DefaultServiceEndpoint), credential, serverEndpoint: serverEndpoint1);
        Assert.Equal(ServiceEndpoint.BuildEndpointString(DefaultServiceEndpointUri), endpoint.Endpoint);
        key = Assert.IsType<MicrosoftEntraAccessKey>(endpoint.AccessKey);
        Assert.Equal(serverEndpoint1, endpoint.ServerEndpoint);
        Assert.Equal("http://serverEndpoint:123/api/v1/auth/accessKey", key.GetAccessKeyUrl, StringComparer.OrdinalIgnoreCase);

        // use object initializer
        var serverEndpoint2 = new Uri("http://serverEndpoint:123/path");
        endpoint = new ServiceEndpoint(new Uri(DefaultServiceEndpoint), credential)
        {
            ServerEndpoint = serverEndpoint2
        };
        Assert.Equal(ServiceEndpoint.BuildEndpointString(DefaultServiceEndpointUri), endpoint.Endpoint);
        key = Assert.IsType<MicrosoftEntraAccessKey>(endpoint.AccessKey);
        Assert.Equal(serverEndpoint2, endpoint.ServerEndpoint);
        Assert.Equal("http://serverEndpoint:123/path/api/v1/auth/accessKey", key.GetAccessKeyUrl, StringComparer.OrdinalIgnoreCase);

        // object initializer should override constructor param
        endpoint = new ServiceEndpoint(new Uri(DefaultServiceEndpoint), credential, serverEndpoint: serverEndpoint1)
        {
            ServerEndpoint = serverEndpoint2
        };
        Assert.Equal(ServiceEndpoint.BuildEndpointString(DefaultServiceEndpointUri), endpoint.Endpoint);
        key = Assert.IsType<MicrosoftEntraAccessKey>(endpoint.AccessKey);
        Assert.Equal(serverEndpoint2, endpoint.ServerEndpoint);
        Assert.Equal("http://serverEndpoint:123/path/api/v1/auth/accessKey", key.GetAccessKeyUrl, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestAzureCredentialConstructorWithClientEndpoint()
    {
        var credential = new DefaultAzureCredential();

        // use default endpoint
        var endpoint = new ServiceEndpoint(new Uri(DefaultServiceEndpoint), credential);
        Assert.Equal(ServiceEndpoint.BuildEndpointString(DefaultServiceEndpointUri), endpoint.Endpoint);
        Assert.IsType<MicrosoftEntraAccessKey>(endpoint.AccessKey);
        Assert.Equal(DefaultServiceEndpointUri, endpoint.ClientEndpoint);

        // throw if client endpoint is invalid
        var invalidUri = new Uri("ftp://foo");
        Assert.Throws<ArgumentException>(() => new ServiceEndpoint(invalidUri, credential));
        Assert.Throws<ArgumentException>(() => new ServiceEndpoint(new Uri(DefaultServiceEndpoint), credential, clientEndpoint: invalidUri));
        Assert.Throws<ArgumentException>(() => new ServiceEndpoint(new Uri(DefaultServiceEndpoint), credential)
        {
            ClientEndpoint = invalidUri,
        });

        // use constructor param.
        var clientEndpoint1 = new Uri("https://clientEndpoint:123");
        endpoint = new ServiceEndpoint(new Uri(DefaultServiceEndpoint), credential, clientEndpoint: clientEndpoint1);
        Assert.Equal(ServiceEndpoint.BuildEndpointString(DefaultServiceEndpointUri), endpoint.Endpoint);
        Assert.IsType<MicrosoftEntraAccessKey>(endpoint.AccessKey);
        Assert.Equal(clientEndpoint1, endpoint.ClientEndpoint);

        // use object initializer.
        var clientEndpoint2 = new Uri("https://clientEndpoint:123/path");
        endpoint = new ServiceEndpoint(new Uri(DefaultServiceEndpoint), credential)
        {
            ClientEndpoint = clientEndpoint2,
        };
        Assert.Equal(ServiceEndpoint.BuildEndpointString(DefaultServiceEndpointUri), endpoint.Endpoint);
        Assert.IsType<MicrosoftEntraAccessKey>(endpoint.AccessKey);
        Assert.Equal(clientEndpoint2, endpoint.ClientEndpoint);

        // object initializer should override constructor param.
        endpoint = new ServiceEndpoint(new Uri(DefaultServiceEndpoint), credential, clientEndpoint: clientEndpoint1)
        {
            ClientEndpoint = clientEndpoint2,
        };
        Assert.Equal(ServiceEndpoint.BuildEndpointString(DefaultServiceEndpointUri), endpoint.Endpoint);
        Assert.IsType<MicrosoftEntraAccessKey>(endpoint.AccessKey);
        Assert.Equal(clientEndpoint2, endpoint.ClientEndpoint);
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

    public class EndpointEndWithSlash : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] { $"endpoint={HttpEndpoint};accesskey={DefaultKey}", HttpEndpoint };
            yield return new object[] { $"endpoint={HttpEndpoint}/;accesskey={DefaultKey}", HttpEndpoint };
            yield return new object[] { $"endpoint={HttpsEndpoint};accesskey={DefaultKey}", HttpsEndpoint };
            yield return new object[] { $"endpoint={HttpsEndpoint}/;accesskey={DefaultKey}", HttpsEndpoint };
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class EndpointAndPortTestData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            // http
            yield return new object[] { $"endpoint={HttpEndpoint};accesskey={DefaultKey}", HttpEndpoint + "/", HttpEndpoint };
            yield return new object[] { $"endpoint={HttpEndpoint}:80;accesskey={DefaultKey}", HttpEndpoint + "/", HttpEndpoint };
            yield return new object[] { $"endpoint={HttpEndpoint}:500;accesskey={DefaultKey}", HttpEndpoint + "/", HttpEndpoint + ":500" };

            // https
            yield return new object[] { $"endpoint={HttpsEndpoint};accesskey={DefaultKey}", HttpsEndpoint + "/", HttpsEndpoint };
            yield return new object[] { $"endpoint={HttpsEndpoint}:443;accesskey={DefaultKey}", HttpsEndpoint + "/", HttpsEndpoint };
            yield return new object[] { $"endpoint={HttpsEndpoint}:500;accesskey={DefaultKey}", HttpsEndpoint + "/", HttpsEndpoint + ":500" };

            // uppercase endpoint
            yield return new object[] { $"endpoint={HttpEndpoint.ToUpper()};accesskey={DefaultKey}", HttpEndpoint + "/", HttpEndpoint };
            yield return new object[] { $"endpoint={HttpsEndpoint.ToUpper()};accesskey={DefaultKey}", HttpsEndpoint + "/", HttpsEndpoint };

            // port override
            yield return new object[] { $"endpoint={HttpsEndpoint};accesskey={DefaultKey};port=500", HttpsEndpoint + "/", HttpsEndpoint + ":500" };
            yield return new object[] { $"endpoint={HttpsEndpoint}:500;accesskey={DefaultKey};port=443", HttpsEndpoint + "/", HttpsEndpoint };

            // uppercase property name
            yield return new object[] { $"ENDPOINT={HttpEndpoint};ACCESSKEY={DefaultKey}", HttpEndpoint + "/", HttpEndpoint };
            yield return new object[] { $"ENDPOINT={HttpsEndpoint}:500;ACCESSKEY={DefaultKey};PORT=443", HttpsEndpoint + "/", HttpsEndpoint };
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class ClientEndpointTestData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] { $"endpoint={HttpEndpoint};authType=aad;clientEndpoint={HttpClientEndpoint}", HttpClientEndpoint };
            yield return new object[] { $"endpoint={HttpEndpoint};authType=aad;clientEndpoint={HttpClientEndpoint}:80", HttpClientEndpoint + ":80" };
            yield return new object[] { $"endpoint={HttpsEndpoint};authType=aad;clientEndpoint={HttpsClientEndpoint}", HttpsClientEndpoint };
            yield return new object[] { $"endpoint={HttpsEndpoint};authType=aad;clientEndpoint={HttpsClientEndpoint}:443", HttpsClientEndpoint + ":443" };
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
