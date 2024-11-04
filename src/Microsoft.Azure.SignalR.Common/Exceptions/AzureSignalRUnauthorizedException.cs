// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Azure.SignalR.Common;

#nullable enable

[Serializable]
public class AzureSignalRUnauthorizedException : AzureSignalRException
{
    [Obsolete]
    internal const string ErrorMessage = $"401 Unauthorized. If you were using AccessKey, {ErrorMessageLocalAuth}. If you were using Microsoft Entra authentication, {ErrorMessageMicrosoftEntra}";

    internal const string ErrorMessageLocalAuth = "please check the connection string and see if the AccessKey is correct.";

    internal const string ErrorMessageMicrosoftEntra = "please check if you set the AzureAuthorityHosts properly in your TokenCredentialOptions, see \"https://learn.microsoft.com/en-us/dotnet/api/azure.identity.azureauthorityhosts\".";

    [Obsolete]
    public AzureSignalRUnauthorizedException(string? requestUri, Exception innerException) : base(string.IsNullOrEmpty(requestUri) ? ErrorMessage : $"{ErrorMessage} Request Uri: {requestUri}", innerException)
    {
    }

    internal AzureSignalRUnauthorizedException(string? requestUri, Exception innerException, string? jwtToken) : base(GetExceptionMessage(jwtToken, requestUri), innerException)
    {
    }

    private static string GetExceptionMessage(string? jwtToken, string? requestUri)
    {
        var message = TokenUtilities.TryParseIssuer(jwtToken, out var iss)
            ? Constants.AsrsTokenIssuer.Equals(iss) ? ErrorMessageLocalAuth : ErrorMessageMicrosoftEntra
            : ErrorMessageLocalAuth;
        return string.IsNullOrEmpty(requestUri) ? $"401 Unauthorized, {message}" : $"401 Unauthorized, {message} Request Uri: {requestUri}";
    }

#if NET8_0_OR_GREATER
    [Obsolete]
#endif
    protected AzureSignalRUnauthorizedException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}