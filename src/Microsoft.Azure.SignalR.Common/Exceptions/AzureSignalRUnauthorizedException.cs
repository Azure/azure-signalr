// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Azure.SignalR.Common;

[Serializable]
public class AzureSignalRUnauthorizedException : AzureSignalRException
{
    /// <summary>
    /// TODO: Try parse token issuer to identify the possible cause of 401
    /// </summary>
    internal const string ErrorMessage = $"401 Unauthorized. If you were using AccessKey, {ErrorMessageLocalAuth}. If you were using Microsoft Entra authentication, {ErrorMessageMicrosoftEntra}";

    internal const string ErrorMessageLocalAuth = "please check the connection string and see if the AccessKey is correct.";

    internal const string ErrorMessageMicrosoftEntra = "please check if you set the AzureAuthorityHosts properly in your TokenCredentialOptions, see \"https://learn.microsoft.com/en-us/dotnet/api/azure.identity.azureauthorityhosts\".";

    public AzureSignalRUnauthorizedException(string requestUri, Exception innerException) : base(string.IsNullOrEmpty(requestUri) ? ErrorMessage : $"{ErrorMessage} Request Uri: {requestUri}", innerException)
    {
    }

    internal AzureSignalRUnauthorizedException(Exception innerException) : base(ErrorMessage, innerException)
    {
    }

#if NET8_0_OR_GREATER
    [Obsolete]
#endif
    protected AzureSignalRUnauthorizedException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}