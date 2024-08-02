// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
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

    [Theory]
    // For Linux and Mac, `CultureInfo` ctor doesn't treat invalid culture string as an invalid one. See https://github.com/dotnet/runtime/issues/11590
    // If `CultureInfo` ctor treats the culture string as an invalid one, the default culture won't be changed and `parsedCulture` will be ignored.
    // Culture / UICulture by default is same as OS, typically en-US. See https://learn.microsoft.com/en-us/dotnet/api/system.threading.thread.currentuiculture?view=net-8.0&redirectedfrom=MSDN#remarks
    [InlineData("&asrs_lang=ar-SA", true, "ar-SA", false, null)]
    [InlineData("&asrs_lang=zh-CN", true, "zh-CN", false, null)]
    [InlineData("&asrs_ui_lang=ar-SA", false, null, true, "ar-SA")]
    [InlineData("&asrs_ui_lang=zh-CN", false, null, true, "zh-CN")]
    [InlineData("&asrs_lang=ar-SA&asrs_ui_lang=zh-CN", true, "ar-SA", true, "zh-CN")]
    [InlineData("&asrs_lang=zh-CN&asrs_ui_lang=ar-SA", true, "zh-CN", true, "ar-SA")]
#if OS_WINDOWS
    [InlineData("", false, null, false, null)]
    [InlineData("&arsa_lang=", false, null, false, null)]
    [InlineData("&arsa_lang=123", false, null, false, null)]
    [InlineData("&arsa_ui_lang=", false, null, false, null)]
    [InlineData("&arsa_ui_lang=123", false, null, false, null)]
    [InlineData("&asrs_lang=ar-SA&asrs_ui_lang=", true, "ar-SA", false, null)]
    [InlineData("&asrs_lang=ar-SA&asrs_ui_lang=123", true, "ar-SA", false, null)]
    [InlineData("&asrs_lang=&asrs_ui_lang=ar-SA", false, null, true, "ar-SA")]
    [InlineData("&asrs_lang=123&asrs_ui_lang=ar-SA", false, null, true, "ar-SA")]
#endif
    public void ServiceConnectionContextCultureTest(string cultureQuery, bool isCultureValid, string parsedCulture, bool isUiCultureValid, string parsedUiCulture)
    {
        var queryString = $"?{cultureQuery}";
        var originalCulture = CultureInfo.CurrentCulture.Name;
        var originalUiCulture = CultureInfo.CurrentUICulture.Name;

        _ = new ClientConnectionContext(new OpenConnectionMessage("1", new Claim[0], EmptyHeaders, queryString));
        
        var expectedCulture = isCultureValid ? parsedCulture : originalCulture;
        var expectedUiCulture = isUiCultureValid ? parsedUiCulture : originalUiCulture;

        Assert.Equal(expectedCulture, CultureInfo.CurrentCulture.Name);
        Assert.Equal(expectedUiCulture, CultureInfo.CurrentUICulture.Name);
    }
}
