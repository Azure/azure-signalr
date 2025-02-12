// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests;

public class ServiceContextFacts
{
    private static readonly IDictionary<string, StringValues> EmptyHeaders = new Dictionary<string, StringValues>();

    [Fact]
    public void ServiceConnectionContextWithEmptyClaimsIsUnauthenticated()
    {
        var serviceConnectionContext = new ClientConnectionContext(new OpenConnectionMessage("1", new Claim[0]));
        Assert.NotNull(serviceConnectionContext.User.Identity);
        Assert.False(serviceConnectionContext.User.Identity.IsAuthenticated);
    }

    [Fact]
    public void ServiceConnectionContextWithNullClaimsIsUnauthenticated()
    {
        var serviceConnectionContext = new ClientConnectionContext(new OpenConnectionMessage("1", null));
        Assert.NotNull(serviceConnectionContext.User.Identity);
        Assert.False(serviceConnectionContext.User.Identity.IsAuthenticated);
    }

    [Fact]
    public void ServiceConnectionContextWithSystemClaimsIsUnauthenticated()
    {
        var claims = new[]
        {
            new Claim("aud", "http://localhost"),
            new Claim("exp", "1234567890"),
            new Claim("iat", "1234567890"),
            new Claim("nbf", "1234567890"),
            new Claim(Constants.ClaimType.UserId, "customUserId"),
        };
        var serviceConnectionContext = new ClientConnectionContext(new OpenConnectionMessage("1", claims));
        Assert.NotNull(serviceConnectionContext.User.Identity);
        Assert.False(serviceConnectionContext.User.Identity.IsAuthenticated);
    }

    [Fact]
    public void ServiceConnectionContextWithCustomNameTypeIsAuthenticated()
    {
        var claims = new[]
        {
            new Claim("aud", "http://localhost"),
            new Claim("exp", "1234567890"),
            new Claim("iat", "1234567890"),
            new Claim("nbf", "1234567890"),
            new Claim("customNameType", "customUserName"),
            new Claim("customRoleType", "customRole"),
            new Claim(Constants.ClaimType.NameType, "customNameType"),
            new Claim(Constants.ClaimType.RoleType, "customRoleType"),
        };
        var serviceConnectionContext = new ClientConnectionContext(new OpenConnectionMessage("1", claims));
        Assert.NotNull(serviceConnectionContext.User.Identity);
        Assert.False(serviceConnectionContext.User.IsInRole("Admin"));
        Assert.True(serviceConnectionContext.User.IsInRole("customRole"));
        Assert.Equal("customUserName", serviceConnectionContext.User.Identity.Name);
        Assert.True(serviceConnectionContext.User.Identity.IsAuthenticated);
    }

    [Fact]
    public void ServiceConnectionContextWithClaimsCreatesIdentityWithClaims()
    {
        var claims = new[]
        {
            new Claim("k1", "v1"),
            new Claim("k2", "v2")
        };
        var serviceConnectionContext = new ClientConnectionContext(new OpenConnectionMessage("1", claims));
        Assert.NotNull(serviceConnectionContext.User.Identity);
        Assert.True(serviceConnectionContext.User.Identity.IsAuthenticated);
        var contextClaims = serviceConnectionContext.User.Claims.ToList();
        Assert.Equal("k1", contextClaims[0].Type);
        Assert.Equal("v1", contextClaims[0].Value);
        Assert.Equal("k2", contextClaims[1].Type);
        Assert.Equal("v2", contextClaims[1].Value);
    }

    [Fact]
    public void ServiceConnectionContextWithEmptyHttpContextByDefault()
    {
        var serviceConnectionContext = new ClientConnectionContext(new OpenConnectionMessage("1", new Claim[0]));
        Assert.NotNull(serviceConnectionContext.User.Identity);
        Assert.NotNull(serviceConnectionContext.HttpContext);
        Assert.Equal(serviceConnectionContext.User, serviceConnectionContext.HttpContext.User);
        Assert.Empty(serviceConnectionContext.HttpContext.Request.Headers);
        Assert.Empty(serviceConnectionContext.HttpContext.Request.Query);
    }

    [Fact]
    public void ServiceConnectionContextWithNonEmptyHeaders()
    {
        const string key1 = "header-key-1";
        const string key2 = "header-key-2";
        const string value1 = "header-value-1";
        var value2 = new[] { "header-value-2a", "header-value-2b" };
        var serviceConnectionContext = new ClientConnectionContext(new OpenConnectionMessage("1", new Claim[0],
            new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase)
            {
                {key1, value1},
                {key2, value2}
            }, string.Empty));

        Assert.NotNull(serviceConnectionContext.User.Identity);
        Assert.NotNull(serviceConnectionContext.HttpContext);
        Assert.Equal(serviceConnectionContext.User, serviceConnectionContext.HttpContext.User);
        var request = serviceConnectionContext.HttpContext.Request;
        Assert.Equal(2, request.Headers.Count);
        Assert.Equal(value1, request.Headers[key1]);
        Assert.Equal(value2, request.Headers[key2]);
        Assert.Empty(request.Query);
        Assert.Equal(string.Empty, request.Path);
    }

    [Fact]
    public void ServiceConnectionContextWithNonEmptyQueries()
    {
        const string queryString = "?query1=value1&query2=value2&query3=value3";
        var serviceConnectionContext = new ClientConnectionContext(new OpenConnectionMessage("1", new Claim[0], EmptyHeaders, queryString));

        Assert.NotNull(serviceConnectionContext.User.Identity);
        Assert.NotNull(serviceConnectionContext.HttpContext);
        Assert.Equal(serviceConnectionContext.User, serviceConnectionContext.HttpContext.User);
        var request = serviceConnectionContext.HttpContext.Request;
        Assert.Empty(request.Headers);
        Assert.Equal(queryString, request.QueryString.Value);
        Assert.Equal(3, request.Query.Count);
        Assert.Equal("value1", request.Query["query1"]);
        Assert.Equal("value2", request.Query["query2"]);
        Assert.Equal("value3", request.Query["query3"]);
        Assert.Equal(string.Empty, request.Path);
    }

    [Fact]
    public void ServiceConnectionContextWithRequestPath()
    {
        const string path = "/this/is/user/path";
        var queryString = $"?{Constants.QueryParameter.OriginalPath}={WebUtility.UrlEncode(path)}";
        var serviceConnectionContext = new ClientConnectionContext(new OpenConnectionMessage("1", null, EmptyHeaders, queryString));

        Assert.NotNull(serviceConnectionContext.User.Identity);
        Assert.NotNull(serviceConnectionContext.HttpContext);
        Assert.Equal(serviceConnectionContext.User, serviceConnectionContext.HttpContext.User);
        var request = serviceConnectionContext.HttpContext.Request;
        Assert.Empty(request.Headers);
        Assert.Equal(1, request.Query.Count);
        Assert.Equal(path, request.Query[Constants.QueryParameter.OriginalPath]);
        Assert.Equal(path, request.Path);
    }

    [Fact]
    public void ServiceConnectionShouldBeMigrated()
    {
        var isMigratedProperty = typeof(ClientConnectionContext).GetField("_isMigrated", BindingFlags.Instance | BindingFlags.NonPublic);

        var open = new OpenConnectionMessage("foo", Array.Empty<Claim>());
        var context = new ClientConnectionContext(open);
        Assert.False((bool)isMigratedProperty.GetValue(context));

        open.Headers = new Dictionary<string, StringValues>{
            { Constants.AsrsMigrateFrom, "another-server" }
        };
        context = new ClientConnectionContext(open);
        Assert.True((bool)isMigratedProperty.GetValue(context));
    }

    [Theory]
    [InlineData("1.1.1.1", true, "1.1.1.1")]
    [InlineData("1.1.1.1, 2.2.2.2", true, "1.1.1.1")]
    [InlineData("1.1.1.1,2.2.2.2,3.3.3.3", true, "1.1.1.1")]
    [InlineData("2001:db8:cafe::17", true, "2001:db8:cafe::17")]
    [InlineData("256.256.256.256", false, null)]
    [InlineData("", false, null)]
    public void ServiceConnectionContextRemoteIpTest(string xff, bool canBeParsed, string remoteIP)
    {
        var headers = new HeaderDictionary(new Dictionary<string, StringValues>
        {
            ["X-Forwarded-For"] = new StringValues(xff)
        });

        Assert.Equal(canBeParsed, ClientConnectionContext.TryGetRemoteIpAddress(headers, out var address));

        if (canBeParsed)
        {
            Assert.Equal(remoteIP, address.ToString());
        }
    }

    public static IEnumerable<object[]> TestCultures => new object[][]
    {
            new object[]
            {
                new CultureInfo("zh-CN"), new CultureInfo("en-US")
            },
            new object[]
            {
                new CultureInfo("zh-CN") { DateTimeFormat = { ShortDatePattern = "MM#dd#yyyy" } }, 
                new CultureInfo("fr-CA") { DateTimeFormat = { ShortDatePattern = "yyyy|MM|dd" } }
            }
    };

    [Theory]
    [MemberData(nameof(TestCultures))]
    public async Task ServiceConnectionContextCultureManagerTest(CultureInfo expectCulture, CultureInfo expectUICulture)
    {
        var (originalCulture, originalUICulture) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);

        var timeoutSeconds = 1;
        var cultureManager = new DefaultCultureFeatureManager(timeoutSeconds);

        var requestIdProvider = new DefaultConnectionRequestIdProvider();
        var requestId1 = "1";
        var requestId2 = "2";
        var expectFeature = new RequestCultureFeature(new RequestCulture(expectCulture, expectUICulture), null);
        cultureManager.TryAddCultureFeature(requestId1, expectFeature);
        cultureManager.TryAddCultureFeature(requestId2, expectFeature);
        cultureManager.Cleanup(); // should take no effect

        Assert.True(cultureManager.TryRemoveCultureFeature(requestId1, out var actualFeature));
        Assert.Equal(expectFeature, actualFeature);

        await Task.Delay(timeoutSeconds * 1000 + 100);
        cultureManager.Cleanup();
        Assert.False(cultureManager.TryRemoveCultureFeature(requestId2, out var _));
    }
}
