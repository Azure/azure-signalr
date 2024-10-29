// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Runtime.Serialization;

namespace Microsoft.Azure.SignalR.Common;

#nullable enable

[Serializable]
public class AzureSignalRRuntimeException : AzureSignalRException
{
    internal const string ErrorMessage = "Azure SignalR service runtime error.";

    internal const string NetworkErrorMessage = "403 Forbidden, please check your service Networking settings.";

    internal HttpStatusCode StatusCode { get; private set; } = HttpStatusCode.InternalServerError;

    public AzureSignalRRuntimeException(string? requestUri, Exception inner) : base(GetExceptionMessage(HttpStatusCode.InternalServerError, requestUri, string.Empty), inner)
    {
    }

    internal AzureSignalRRuntimeException(string? requestUri, Exception inner, HttpStatusCode statusCode, string? content) : base(GetExceptionMessage(statusCode, requestUri, content), inner)
    {
        StatusCode = statusCode;
    }

    private static string GetExceptionMessage(HttpStatusCode statusCode, string? requestUri, string? content)
    {
        var reason = statusCode switch
        {
            HttpStatusCode.Forbidden => GetForbiddenReason(content ?? string.Empty),
            _ => ErrorMessage,
        };
        return string.IsNullOrEmpty(requestUri) ? reason : $"{reason} Request Uri: {requestUri}";
    }

    private static string GetForbiddenReason(string content)
    {
        return content.Contains("nginx") ? NetworkErrorMessage : content;
    }

#if NET8_0_OR_GREATER
    [Obsolete]
#endif
    protected AzureSignalRRuntimeException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}