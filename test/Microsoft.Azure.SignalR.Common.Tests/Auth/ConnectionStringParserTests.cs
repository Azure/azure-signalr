// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Azure.Identity;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.Auth;

#nullable enable

[Collection("Auth")]
public class ConnectionStringParserTests
{
    private const string DefaultKey = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private const string HttpEndpoint = "http://aaa";

    private const string HttpsEndpoint = "https://aaa";

    private const string ClientEndpoint = "http://bbb";

    private const string ServerEndpoint = "http://ccc";

    private const string TestTenantId = "aaaaaaaa-bbbb-bbbb-bbbb-cccccccccccc";

    private const string TestEndpoint = "https://aaa";

    public static IEnumerable<object?[]> ServerEndpointTestData
    {
        get
        {
            yield return new object?[] { $"endpoint={HttpEndpoint};accesskey={DefaultKey}", null, null };
            yield return new object?[] { $"endpoint={HttpEndpoint}:500;accesskey={DefaultKey}", null, null };
            yield return new object?[] { $"endpoint={HttpEndpoint}:500;accesskey={DefaultKey};port=400", null, null };
            yield return new object?[] { $"endpoint={HttpEndpoint}:500;accesskey={DefaultKey};port=400;serverEndpoint={ServerEndpoint}", ServerEndpoint, 80 };
            yield return new object?[] { $"endpoint={HttpEndpoint}:500;accesskey={DefaultKey};port=400;serverEndpoint={ServerEndpoint}:500", $"{ServerEndpoint}:500", 500 };
        }
    }

    public static IEnumerable<object?[]> ClientEndpointTestData
    {
        get
        {
            yield return new object?[] { $"endpoint={HttpEndpoint};accesskey={DefaultKey}", null, null };
            yield return new object?[] { $"endpoint={HttpEndpoint}:500;accesskey={DefaultKey}", null, null };
            yield return new object?[] { $"endpoint={HttpEndpoint}:500;accesskey={DefaultKey};port=400", null, null };
            yield return new object?[] { $"endpoint={HttpEndpoint}:500;accesskey={DefaultKey};port=400;clientEndpoint={ClientEndpoint}", ClientEndpoint, 80 };
            yield return new object?[] { $"endpoint={HttpEndpoint}:500;accesskey={DefaultKey};port=400;clientEndpoint={ClientEndpoint}:500", $"{ClientEndpoint}:500", 500 };
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
    [MemberData(nameof(ClientEndpointTestData))]
    public void TestClientEndpoint(string connectionString, string expectedClientEndpoint, int? expectedPort)
    {
        var r = ConnectionStringParser.Parse(connectionString);
        var expectedUri = expectedClientEndpoint == null ? null : new Uri(expectedClientEndpoint);
        Assert.Equal(expectedUri, r.ClientEndpoint);
        Assert.Equal(expectedPort, r.ClientEndpoint?.Port);
    }

    [Theory]
    [MemberData(nameof(ServerEndpointTestData))]
    public void TestServerEndpoint(string connectionString, string expectedServerEndpoint, int? expectedPort)
    {
        var r = ConnectionStringParser.Parse(connectionString);
        var expectedUri = expectedServerEndpoint == null ? null : new Uri(expectedServerEndpoint);
        Assert.Equal(expectedUri, r.ServerEndpoint);
        Assert.Equal(expectedPort, r.ServerEndpoint?.Port);
    }

    [Theory]
    [InlineData($"endpoint=https://aaa;AuthType=aad;clientId=foo;clientSecret=bar;tenantId={TestTenantId}")]
    [InlineData($"endpoint=https://aaa;AuthType=azure.app;clientId=foo;clientSecret=bar;tenantId={TestTenantId}")]
    public void TestClientSecretCredential(string connectionString)
    {
        var r = ConnectionStringParser.Parse(connectionString);
        Assert.Null(r.AccessKey);
        Assert.Null(r.ClientEndpoint);
        Assert.Null(r.ServerEndpoint);
        var credential = Assert.IsType<ClientSecretCredential>(r.TokenCredential);

        var tenantIdField = typeof(ClientSecretCredential).GetProperty("TenantId", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.Equal(TestTenantId, Assert.IsType<string>(tenantIdField?.GetValue(credential)));

        var clientIdField = typeof(ClientSecretCredential).GetProperty("ClientId", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.Equal("foo", Assert.IsType<string>(clientIdField?.GetValue(credential)));

        var clientSecretField = typeof(ClientSecretCredential).GetProperty("ClientSecret", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.Equal("bar", Assert.IsType<string>(clientSecretField?.GetValue(credential)));
    }

    [Theory]
    [InlineData($"endpoint=https://aaa;AuthType=aad;clientId=foo;clientCert=bar;tenantId={TestTenantId}")]
    [InlineData($"endpoint=https://aaa;AuthType=azure.app;clientId=foo;clientCert=bar;tenantId={TestTenantId}")]
    public void TestClientCertificateCredential(string connectionString)
    {
        var r = ConnectionStringParser.Parse(connectionString);
        Assert.Null(r.AccessKey);
        Assert.Null(r.ClientEndpoint);
        Assert.Null(r.ServerEndpoint);
        var credential = Assert.IsType<ClientCertificateCredential>(r.TokenCredential);

        var tenantIdField = typeof(ClientCertificateCredential).GetProperty("TenantId", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.Equal(TestTenantId, Assert.IsType<string>(tenantIdField?.GetValue(credential)));

        var clientIdField = typeof(ClientCertificateCredential).GetProperty("ClientId", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.Equal("foo", Assert.IsType<string>(clientIdField?.GetValue(credential)));
    }

    [Theory]
    [InlineData($"endpoint={TestEndpoint};AuthType=azure;clientId=xxxx;")] // should ignore the clientId
    [InlineData($"endpoint={TestEndpoint};AuthType=azure;tenantId=xxxx;")] // should ignore the tenantId
    [InlineData($"endpoint={TestEndpoint};AuthType=azure;clientSecret=xxxx;")] // should ignore the clientSecret
    internal void TestDefaultAzureCredential(string connectionString)
    {
        var r = ConnectionStringParser.Parse(connectionString);
        Assert.Equal(TestEndpoint, r.Endpoint.AbsoluteUri.TrimEnd('/'));

        Assert.Null(r.AccessKey);
        Assert.Null(r.ClientEndpoint);
        Assert.Null(r.ServerEndpoint);
        Assert.IsType<DefaultAzureCredential>(r.TokenCredential);
    }

    [Theory]
    [InlineData($"endpoint={TestEndpoint};AuthType=aad;")]
    [InlineData($"endpoint={TestEndpoint};AuthType=aad;clientId=123;")]
    [InlineData($"endpoint={TestEndpoint};AuthType=aad;tenantId=xxxx;")] // should ignore the tenantId
    [InlineData($"endpoint={TestEndpoint};AuthType=aad;clientSecret=xxxx;")] // should ignore the clientSecret
    [InlineData($"endpoint={TestEndpoint};AuthType=azure.msi;")]
    [InlineData($"endpoint={TestEndpoint};AuthType=azure.msi;clientId=123;")]
    internal void TestManagedIdentityCredential(string connectionString)
    {
        var r = ConnectionStringParser.Parse(connectionString);
        Assert.Equal(TestEndpoint, r.Endpoint.AbsoluteUri.TrimEnd('/'));

        Assert.Null(r.AccessKey);
        Assert.Null(r.ClientEndpoint);
        Assert.Null(r.ServerEndpoint);
        Assert.IsType<ManagedIdentityCredential>(r.TokenCredential);
    }

    [Theory]
    [Obsolete]
    [InlineData("endpoint=https://aaa;AuthType=aad;serverendpoint=https://foo", "https://foo/api/v1/auth/accesskey")]
    [InlineData("endpoint=https://aaa;AuthType=aad;serverendpoint=https://foo:123", "https://foo:123/api/v1/auth/accesskey")]
    [InlineData("endpoint=https://aaa;AuthType=aad;serverendpoint=https://foo/bar", "https://foo/bar/api/v1/auth/accesskey")]
    [InlineData("endpoint=https://aaa;AuthType=aad;serverendpoint=https://foo/bar/", "https://foo/bar/api/v1/auth/accesskey")]
    [InlineData("endpoint=https://aaa;AuthType=aad;serverendpoint=https://foo:123/bar/", "https://foo:123/bar/api/v1/auth/accesskey")]
    internal void TestAzureADWithServerEndpoint(string connectionString, string expectedAuthorizeUrl)
    {
        var endpoint = new ServiceEndpoint(connectionString);
        var key = Assert.IsType<MicrosoftEntraAccessKey>(endpoint.AccessKey);
        Assert.Equal(expectedAuthorizeUrl, key.GetAccessKeyUrl, StringComparer.OrdinalIgnoreCase);
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
}