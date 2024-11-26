// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;

using Azure.Identity;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.Auth;

[Collection("Auth")]
public class ConnectionStringParserTests
{
    private const string ClientEndpoint = "http://bbb";

    private const string DefaultKey = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private const string HttpEndpoint = "http://aaa";

    private const string HttpsEndpoint = "https://aaa";

    private const string ServerEndpoint = "http://ccc";

    public static IEnumerable<object[]> ServerEndpointTestData
    {
        get
        {
            yield return new object[] { $"endpoint={HttpEndpoint};accesskey={DefaultKey}", null, null };
            yield return new object[] { $"endpoint={HttpEndpoint}:500;accesskey={DefaultKey}", null, null };
            yield return new object[] { $"endpoint={HttpEndpoint}:500;accesskey={DefaultKey};port=400", null, null };
            yield return new object[] { $"endpoint={HttpEndpoint}:500;accesskey={DefaultKey};port=400;serverEndpoint={ServerEndpoint}", ServerEndpoint, 80 };
            yield return new object[] { $"endpoint={HttpEndpoint}:500;accesskey={DefaultKey};port=400;serverEndpoint={ServerEndpoint}:500", $"{ServerEndpoint}:500", 500 };
        }
    }

    [Theory]
    [InlineData("endpoint=https://aaa;AuthType=aad;clientId=123;tenantId=aaaaaaaa-bbbb-bbbb-bbbb-cccccccccccc")]
    [InlineData("endpoint=https://aaa;AuthType=azure.app;clientId=123;tenantId=aaaaaaaa-bbbb-bbbb-bbbb-cccccccccccc")]
    public void InvalidAzureApplication(string connectionString)
    {
        var exception = Assert.Throws<ArgumentException>(() => ConnectionStringParser.Parse(connectionString));
        Assert.Contains("Connection string missing required properties clientSecret or clientCert", exception.Message);
    }

    [Theory]
    [InlineData("endpoint=https://aaa;clientEndpoint=aaa;AccessKey=bbb;")]
    [InlineData("endpoint=https://aaa;ClientEndpoint=aaa;AccessKey=bbb;")]
    public void InvalidClientEndpoint(string connectionString)
    {
        var exception = Assert.Throws<ArgumentException>(() => ConnectionStringParser.Parse(connectionString));
        Assert.Contains("Invalid value for clientEndpoint property, it must be a valid URI. (Parameter 'clientEndpoint')", exception.Message);
    }

    [Theory]
    [InlineData("endpoint=https://aaa;serverEndpoint=aaa;AccessKey=bbb;")]
    [InlineData("endpoint=https://aaa;ServerEndpoint=aaa;AccessKey=bbb;")]
    public void InvalidServerEndpoint(string connectionString)
    {
        var exception = Assert.Throws<ArgumentException>(() => ConnectionStringParser.Parse(connectionString));
        Assert.Contains("Invalid value for serverEndpoint property, it must be a valid URI. (Parameter 'serverEndpoint')", exception.Message);
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
        Assert.Contains("Invalid value for endpoint property, it must be a valid URI. (Parameter 'endpoint')", exception.Message);
    }

    [Theory]
    [InlineData("Endpoint=https://aaa;AccessKey=bbb;version=1.0;port=2.3")]
    [InlineData("Endpoint=https://aaa;AccessKey=bbb;version=1.1;port=1000000")]
    [InlineData("Endpoint=https://aaa;AccessKey=bbb;version=1.0-preview;port=0")]
    public void InvalidPort(string connectionString)
    {
        var exception = Assert.Throws<ArgumentException>(() => ConnectionStringParser.Parse(connectionString));
        Assert.Contains("Invalid value for port property, it must be an positive integer between (0, 65536). (Parameter 'port')", exception.Message);
    }

    [Theory]
    [InlineData("Endpoint=https://aaa;AccessKey=bbb;version=abc", "abc")]
    [InlineData("Endpoint=https://aaa;AccessKey=bbb;version=1.x", "1.x")]
    [InlineData("Endpoint=https://aaa;AccessKey=bbb;version=2.0", "2.0")]
    public void InvalidVersion(string connectionString, string version)
    {
        var exception = Assert.Throws<ArgumentException>(() => ConnectionStringParser.Parse(connectionString));
        Assert.Contains($"Version {version} is not supported.", exception.Message);
    }

    [Theory]
    [InlineData("endpoint=https://aaa;AuthType=aad;clientId=foo;clientSecret=bar;tenantId=aaaaaaaa-bbbb-bbbb-bbbb-cccccccccccc")]
    [InlineData("endpoint=https://aaa;AuthType=azure.app;clientId=foo;clientSecret=bar;tenantId=aaaaaaaa-bbbb-bbbb-bbbb-cccccccccccc")]
    public void TestAzureApplication(string connectionString)
    {
        var r = ConnectionStringParser.Parse(connectionString);

        var key = Assert.IsType<MicrosoftEntraAccessKey>(r.AccessKey);
        Assert.IsType<ClientSecretCredential>(key.TokenCredential);
        Assert.Same(r.Endpoint, r.AccessKey.Endpoint);
        Assert.Null(r.Version);
        Assert.Null(r.ClientEndpoint);
    }

    [Theory]
    [ClassData(typeof(ClientEndpointTestData))]
    public void TestClientEndpoint(string connectionString, string expectedClientEndpoint, int? expectedPort)
    {
        var r = ConnectionStringParser.Parse(connectionString);
        Assert.Same(r.Endpoint, r.AccessKey.Endpoint);
        var expectedUri = expectedClientEndpoint == null ? null : new Uri(expectedClientEndpoint);
        Assert.Equal(expectedUri, r.ClientEndpoint);
        Assert.Equal(expectedPort, r.ClientEndpoint?.Port);
    }

    [Theory]
    [MemberData(nameof(ServerEndpointTestData))]
    public void TestServerEndpoint(string connectionString, string expectedServerEndpoint, int? expectedPort)
    {
        var r = ConnectionStringParser.Parse(connectionString);
        Assert.Same(r.Endpoint, r.AccessKey.Endpoint);
        var expectedUri = expectedServerEndpoint == null ? null : new Uri(expectedServerEndpoint);
        Assert.Equal(expectedUri, r.ServerEndpoint);
        Assert.Equal(expectedPort, r.ServerEndpoint?.Port);
    }

    [Theory]
    [ClassData(typeof(VersionTestData))]
    public void TestVersion(string connectionString, string expectedVersion)
    {
        var r = ConnectionStringParser.Parse(connectionString);
        Assert.Same(r.Endpoint, r.AccessKey.Endpoint);
        Assert.Equal(expectedVersion, r.Version);
    }

    [Theory]
    [InlineData("https://aaa", "endpoint=https://aaa;AuthType=azure;clientId=xxxx;")] // should ignore the clientId
    [InlineData("https://aaa", "endpoint=https://aaa;AuthType=azure;tenantId=xxxx;")] // should ignore the tenantId
    [InlineData("https://aaa", "endpoint=https://aaa;AuthType=azure;clientSecret=xxxx;")] // should ignore the clientSecret
    internal void TestDefaultAzureCredential(string expectedEndpoint, string connectionString)
    {
        var r = ConnectionStringParser.Parse(connectionString);

        Assert.Equal(expectedEndpoint, r.Endpoint.AbsoluteUri.TrimEnd('/'));
        var key = Assert.IsType<MicrosoftEntraAccessKey>(r.AccessKey);
        Assert.IsType<DefaultAzureCredential>(key.TokenCredential);
        Assert.Same(r.Endpoint, r.AccessKey.Endpoint);
    }

    [Theory]
    [InlineData("https://aaa", "endpoint=https://aaa;AuthType=aad;")]
    [InlineData("https://aaa", "endpoint=https://aaa;AuthType=aad;clientId=123;")]
    [InlineData("https://aaa", "endpoint=https://aaa;AuthType=aad;tenantId=xxxx;")] // should ignore the tenantId
    [InlineData("https://aaa", "endpoint=https://aaa;AuthType=aad;clientSecret=xxxx;")] // should ignore the clientSecret
    [InlineData("https://aaa", "endpoint=https://aaa;AuthType=azure.msi;")]
    [InlineData("https://aaa", "endpoint=https://aaa;AuthType=azure.msi;clientId=123;")]
    internal void TestManagedIdentity(string expectedEndpoint, string connectionString)
    {
        var r = ConnectionStringParser.Parse(connectionString);

        Assert.Equal(expectedEndpoint, r.Endpoint.AbsoluteUri.TrimEnd('/'));
        var key = Assert.IsType<MicrosoftEntraAccessKey>(r.AccessKey);
        Assert.IsType<ManagedIdentityCredential>(key.TokenCredential);
        Assert.Same(r.Endpoint, r.AccessKey.Endpoint);
        Assert.Null(r.ClientEndpoint);
    }

    [Theory]
    [InlineData("endpoint=https://aaa;AuthType=aad;serverendpoint=https://foo", "https://foo/api/v1/auth/accesskey")]
    [InlineData("endpoint=https://aaa;AuthType=aad;serverendpoint=https://foo:123", "https://foo:123/api/v1/auth/accesskey")]
    [InlineData("endpoint=https://aaa;AuthType=aad;serverendpoint=https://foo/bar", "https://foo/bar/api/v1/auth/accesskey")]
    [InlineData("endpoint=https://aaa;AuthType=aad;serverendpoint=https://foo/bar/", "https://foo/bar/api/v1/auth/accesskey")]
    [InlineData("endpoint=https://aaa;AuthType=aad;serverendpoint=https://foo:123/bar/", "https://foo:123/bar/api/v1/auth/accesskey")]
    internal void TestAzureADWithServerEndpoint(string connectionString, string expectedAuthorizeUrl)
    {
        var r = ConnectionStringParser.Parse(connectionString);
        var key = Assert.IsType<MicrosoftEntraAccessKey>(r.AccessKey);
        Assert.Equal(expectedAuthorizeUrl, key.GetAccessKeyUrl, StringComparer.OrdinalIgnoreCase);
    }

    public class ClientEndpointTestData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] { $"endpoint={HttpEndpoint};accesskey={DefaultKey}", null, null };
            yield return new object[] { $"endpoint={HttpEndpoint}:500;accesskey={DefaultKey}", null, null };
            yield return new object[] { $"endpoint={HttpEndpoint}:500;accesskey={DefaultKey};port=400", null, null };
            yield return new object[] { $"endpoint={HttpEndpoint}:500;accesskey={DefaultKey};port=400;clientEndpoint={ClientEndpoint}", ClientEndpoint, 80 };
            yield return new object[] { $"endpoint={HttpEndpoint}:500;accesskey={DefaultKey};port=400;clientEndpoint={ClientEndpoint}:500", $"{ClientEndpoint}:500", 500 };
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

    public class VersionTestData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] { $"endpoint={HttpEndpoint};accesskey={DefaultKey}", null };
            yield return new object[] { $"endpoint={HttpEndpoint};accesskey={DefaultKey};version=1.0", "1.0" };
            yield return new object[] { $"endpoint={HttpEndpoint};accesskey={DefaultKey};version=1.1-preview", "1.1-preview" };
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}