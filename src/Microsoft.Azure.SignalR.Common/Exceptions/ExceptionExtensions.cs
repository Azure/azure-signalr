// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;

using Microsoft.Rest;

namespace Microsoft.Azure.SignalR.Common
{
    internal static class ExceptionExtensions
    {
        internal static Exception WrapAsAzureSignalRException(this Exception e, Uri baseUri)
        {
            switch (e)
            {
                case HttpOperationException operationException:
                    var response = operationException.Response;
                    var request = operationException.Request;
                    var detail = response.Content;

                    var innerException = new HttpRequestException(
                        $"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase})", operationException);

                    return response.StatusCode switch
                    {
                        HttpStatusCode.BadRequest => new AzureSignalRInvalidArgumentException(request.RequestUri.ToString(), innerException, detail),
                        HttpStatusCode.Unauthorized => new AzureSignalRUnauthorizedException(request.RequestUri.ToString(), innerException),
                        HttpStatusCode.NotFound => new AzureSignalRInaccessibleEndpointException(request.RequestUri.ToString(), innerException),
                        _ => new AzureSignalRRuntimeException(baseUri.ToString(), innerException),
                    };
                case HttpRequestException requestException:
                    return new AzureSignalRInaccessibleEndpointException(baseUri.ToString(), requestException);
                default:
                    return e;
            }
        }

        internal static Exception WrapAsAzureSignalRException(this Exception e)
        {
            switch (e)
            {
                case WebSocketException webSocketException:
                    if (e.Message.StartsWith("The server returned status code \"401\""))
                    {
                        return new AzureSignalRUnauthorizedException(webSocketException);
                    }
                    return e;
                default: return e;
            }
        }
    }
}