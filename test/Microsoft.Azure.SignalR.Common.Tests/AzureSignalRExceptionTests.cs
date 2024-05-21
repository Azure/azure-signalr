// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Azure.Identity;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests;

public class AzureSignalRExceptionTests
{
    [Fact]
    public void CredentialNotAuthorizedTest()
    {
        var credential = new DefaultAzureCredential();
        var inner = new Exception();
        var exception = new AzureSignalRAccessTokenNotAuthorizedException(credential.GetType().Name, inner);
        Assert.StartsWith(nameof(DefaultAzureCredential), exception.Message);
    }
}
