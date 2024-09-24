﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common;

namespace Microsoft.Azure.SignalR;

#nullable enable

internal partial class MicrosoftEntraAccessKey
{
    internal sealed class RestApiEndpoint
    {
        public string Audience { get; }

        public string Token { get; }

        public RestApiEndpoint(string endpoint, string token)
        {
            Audience = endpoint;
            Token = token;
        }
    }

    internal sealed class RestClient
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public RestClient(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public Task SendAsync(
            RestApiEndpoint api,
            HttpMethod httpMethod,
            Func<HttpResponseMessage, Task<bool>>? handleExpectedResponseAsync = null,
            CancellationToken cancellationToken = default)
        {
            return SendAsyncCore(Constants.HttpClientNames.UserDefault, api, httpMethod, handleExpectedResponseAsync, cancellationToken);
        }

        private async Task ThrowExceptionOnResponseFailureAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var detail = await response.Content.ReadAsStringAsync();

#if NET5_0_OR_GREATER
            var innerException = new HttpRequestException(
    $"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase})", null, response.StatusCode);
#else
            var innerException = new HttpRequestException(
                $"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase})");
#endif
            throw response.StatusCode switch
            {
                HttpStatusCode.BadRequest => new AzureSignalRInvalidArgumentException(response.RequestMessage?.RequestUri?.ToString(), innerException, detail),
                HttpStatusCode.Unauthorized => new AzureSignalRUnauthorizedException(response.RequestMessage?.RequestUri?.ToString(), innerException),
                HttpStatusCode.NotFound => new AzureSignalRInaccessibleEndpointException(response.RequestMessage?.RequestUri?.ToString(), innerException),
                _ => new AzureSignalRRuntimeException(response.RequestMessage?.RequestUri?.ToString(), innerException),
            };
        }

        private async Task SendAsyncCore(
            string httpClientName,
            RestApiEndpoint api,
            HttpMethod httpMethod,
            Func<HttpResponseMessage, Task<bool>>? handleExpectedResponseAsync = null,
            CancellationToken cancellationToken = default)
        {
            using var httpClient = _httpClientFactory.CreateClient(httpClientName);
            using var request = BuildRequest(api, httpMethod);

            try
            {
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (handleExpectedResponseAsync == null)
                {
                    await ThrowExceptionOnResponseFailureAsync(response);
                }
                else
                {
                    if (!await handleExpectedResponseAsync(response))
                    {
                        await ThrowExceptionOnResponseFailureAsync(response);
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                throw new AzureSignalRException($"An error happened when making request to {request.RequestUri}", ex);
            }
        }

        private HttpRequestMessage BuildRequest(RestApiEndpoint api, HttpMethod httpMethod)
        {
            return GenerateHttpRequest(api.Audience, httpMethod, api.Token);
        }

        private HttpRequestMessage GenerateHttpRequest(string url, HttpMethod httpMethod, string tokenString)
        {
            var request = new HttpRequestMessage(httpMethod, new Uri(url));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenString);
            return request;
        }
    }
}