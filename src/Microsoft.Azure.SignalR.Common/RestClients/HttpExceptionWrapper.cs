// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Net.Http;
using Microsoft.Rest;

namespace Microsoft.Azure.SignalR.Common.RestClients
{
    public class HttpExceptionWrapper
    {
        private readonly Uri _baseUri;

        public HttpExceptionWrapper(Uri baseUri)
        {
            _baseUri = baseUri ?? throw new ArgumentNullException(nameof(baseUri));
        }

        public Exception WrapException(Exception e)
        {
            switch (e)
            {
                case HttpOperationException operationException:
                    var response = operationException.Response;
                    var request = operationException.Request;
                    var detail = response.Content;

                    var innerException = new HttpRequestException(
                        $"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase})"); ;

                    return response.StatusCode switch
                    {
                        HttpStatusCode.BadRequest => new AzureSignalRInvalidArgumentException(request.RequestUri.ToString(), innerException, detail),
                        HttpStatusCode.Unauthorized => new AzureSignalRUnauthorizedException(request.RequestUri.ToString(), innerException),
                        HttpStatusCode.NotFound => new AzureSignalRInaccessibleEndpointException(request.RequestUri.ToString(), innerException),
                        _ => new AzureSignalRRuntimeException(request.RequestUri.ToString(), innerException),
                    };
                case HttpRequestException requestException:
                    return new AzureSignalRInaccessibleEndpointException(_baseUri.ToString(), requestException);
                default:
                    return e;
            }

        }
    }
}
