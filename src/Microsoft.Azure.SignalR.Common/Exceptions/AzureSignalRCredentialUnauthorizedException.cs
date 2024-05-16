// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Azure.Core;

namespace Microsoft.Azure.SignalR.Common;

public class AzureSignalRCredentialUnauthorizedException : AzureSignalRException
{
    private const string Postfix = " does not have permission to get access keys";

    public AzureSignalRCredentialUnauthorizedException(TokenCredential credential, Exception innerException) : 
        base(credential.GetType().Name + Postfix, innerException)
    {
    }
}
