// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core.Serialization;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

#nullable enable

namespace Microsoft.Azure.SignalR
{
    internal class RestClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IPayloadContentBuilder _payloadContentBuilder;

        public RestClient(IHttpClientFactory httpClientFactory, IPayloadContentBuilder contentBuilder)
        {
            _httpClientFactory = httpClientFactory;
            _payloadContentBuilder = contentBuilder;
        }

        // TODO: Test only, will remove later
        internal RestClient(IHttpClientFactory httpClientFactory) : this(httpClientFactory, new JsonPayloadContentBuilder(new JsonObjectSerializer()))
        {
        }

        // TODO: remove later
        public RestClient() : this(HttpClientFactory.Instance)
        {
        }

        public Task SendAsync(
            RestApiEndpoint api,
            HttpMethod httpMethod,
            string? methodName = null,
            object[]? args = null,
            Func<HttpResponseMessage, bool>? handleExpectedResponse = null,
            CancellationToken cancellationToken = default)
        {
            if (handleExpectedResponse == null)
            {
                return SendAsync(api, httpMethod, methodName, args, handleExpectedResponseAsync: null, cancellationToken);
            }

            return SendAsync(api, httpMethod, methodName, args, response => Task.FromResult(handleExpectedResponse(response)), cancellationToken);
        }

        public Task SendAsync(
            RestApiEndpoint api,
            HttpMethod httpMethod,
            string? methodName = null,
            object[]? args = null,
            Func<HttpResponseMessage, Task<bool>>? handleExpectedResponseAsync = null,
            CancellationToken cancellationToken = default)
        {
            return SendAsyncCore(Options.DefaultName, api, httpMethod, methodName, args, handleExpectedResponseAsync, cancellationToken);
        }

        public Task SendWithRetryAsync(
            RestApiEndpoint api,
            HttpMethod httpMethod,
            string? methodName = null,
            object[]? args = null,
            Func<HttpResponseMessage, bool>? handleExpectedResponse = null,
            CancellationToken cancellationToken = default)
        {
            return SendAsyncCore(Constants.HttpClientNames.Resilient, api, httpMethod, methodName, args, handleExpectedResponse == null ? null : response => Task.FromResult(handleExpectedResponse(response)), cancellationToken);
        }

        public Task SendMessageWithRetryAsync(
            RestApiEndpoint api,
            HttpMethod httpMethod,
            string? methodName = null,
            object[]? args = null,
            Func<HttpResponseMessage, bool>? handleExpectedResponse = null,
            CancellationToken cancellationToken = default)
        {
            return SendAsyncCore(Constants.HttpClientNames.MessageResilient, api, httpMethod, methodName, args, handleExpectedResponse == null ? null : response => Task.FromResult(handleExpectedResponse(response)), cancellationToken);
        }

        private async Task ThrowExceptionOnResponseFailureAsync(HttpResponseMessage response)
        {
            using var activity = Telemetry.ReceiveResponseEvent(response);
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
            activity?.SetStatus(ActivityStatusCode.Error, detail);
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
            string? methodName = null,
            object[]? args = null,
            Func<HttpResponseMessage, Task<bool>>? handleExpectedResponseAsync = null,
            CancellationToken cancellationToken = default)
        {
            using var httpClient = _httpClientFactory.CreateClient(httpClientName);
            using var request = BuildRequest(api, httpMethod, methodName, args);
            
            using var activity = Telemetry.SendRequestEvent(request);

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
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw new AzureSignalRException($"An error happened when making request to {request.RequestUri}", ex);
            }
        }

        private static Uri GetUri(string url, IDictionary<string, StringValues>? query)
        {
            if (query == null || query.Count == 0)
            {
                return new Uri(url);
            }
            var builder = new UriBuilder(url);
            var sb = new StringBuilder(builder.Query);
            if (sb.Length == 1 && sb[0] == '?')
            {
                sb.Clear();
            }
            else if (sb.Length > 0 && sb[0] != '?')
            {
                sb.Insert(0, '?');
            }
            foreach (var item in query)
            {
                foreach (var value in item.Value)
                {
                    sb.Append(sb.Length > 0 ? '&' : '?');
                    sb.Append(Uri.EscapeDataString(item.Key));
                    sb.Append('=');
                    sb.Append(Uri.EscapeDataString(value!));
                }
            }
            builder.Query = sb.ToString();
            return builder.Uri;
        }

        private HttpRequestMessage BuildRequest(RestApiEndpoint api, HttpMethod httpMethod, string? methodName = null, object[]? args = null)
        {
            var payload = httpMethod == HttpMethod.Post ? new PayloadMessage { Target = methodName, Arguments = args } : null;
            return GenerateHttpRequest(api.Audience, api.Query, httpMethod, payload, api.Token);
        }

        private HttpRequestMessage GenerateHttpRequest(string url, IDictionary<string, StringValues> query, HttpMethod httpMethod, PayloadMessage? payload, string tokenString)
        {
            var request = new HttpRequestMessage(httpMethod, GetUri(url, query));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenString);
            request.Content = _payloadContentBuilder.Build(payload);
            return request;
        }
    }
}