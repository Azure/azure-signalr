// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.Exceptions;

#nullable enable

public class AzureSignalRUnauthorizedExceptionTests
{
    private const string DefaultSigningKey = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public enum TokenType
    {
        Local,

        MicrosoftEntra
    }

    [Theory]
    [InlineData(null, TokenType.MicrosoftEntra, AzureSignalRUnauthorizedException.ErrorMessageMicrosoftEntra)]
    [InlineData("https://foo.bar", TokenType.MicrosoftEntra, AzureSignalRUnauthorizedException.ErrorMessageMicrosoftEntra)]
    [InlineData(null, TokenType.Local, AzureSignalRUnauthorizedException.ErrorMessageLocalAuth)]
    [InlineData("https://foo.bar", TokenType.Local, AzureSignalRUnauthorizedException.ErrorMessageLocalAuth)]
    public void TestExceptionMessage(string? requestUri, TokenType tokenType, string expected)
    {
        var jwtToken = AuthUtility.GenerateJwtToken(Encoding.UTF8.GetBytes(DefaultSigningKey),
                                                    issuer: tokenType == TokenType.Local ? Constants.AsrsTokenIssuer : "microsoft.com");

        var inner = new Exception();
        var exception = new AzureSignalRUnauthorizedException(requestUri, inner, jwtToken);
        Assert.Same(inner, exception.InnerException);
        Assert.StartsWith("401 Unauthorized,", exception.Message);
        Assert.Contains(expected, exception.Message);
        if (requestUri != null)
        {
            Assert.EndsWith($"Request Uri: {requestUri}", exception.Message);
        }
    }
}
