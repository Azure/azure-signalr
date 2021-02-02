// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

namespace Microsoft.Azure.SignalR
{
    internal class RestClient
    {
        public JsonSerializerSettings JsonSerializerSettings { get; set; } = new JsonSerializerSettings();

        public Task SendAsync(
            RestApiEndpoint api,
            HttpMethod httpMethod,
            string productInfo,
            string methodName = null,
            object[] args = null,
            Func<HttpResponseMessage, bool> handleExpectedResponse = null,
            CancellationToken cancellationToken = default)
        {
            if (handleExpectedResponse == null)
            {
                return SendAsync(api, httpMethod, productInfo, methodName, args, handleExpectedResponseAsync: null, cancellationToken);
            }

            return SendAsync(api, httpMethod, productInfo, methodName, args, response => Task.FromResult(handleExpectedResponse(response)), cancellationToken);
        }

        public async Task SendAsync(
            RestApiEndpoint api,
            HttpMethod httpMethod,
            string productInfo,
            string methodName = null,
            object[] args = null,
            Func<HttpResponseMessage, Task<bool>> handleExpectedResponseAsync = null,
            CancellationToken cancellationToken = default)
        {
            var httpClient = HttpClientFactory.CreateClient();
            var request = BuildRequest(api, httpMethod, productInfo, methodName, args);
            HttpResponseMessage response;

            try
            {
                response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                throw new AzureSignalRInaccessibleEndpointException(request.RequestUri.ToString(), ex);
            }

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

            response.Dispose();
        }

        public async Task ThrowExceptionOnResponseFailureAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var detail = await response.Content.ReadAsStringAsync();

            var innerException = new HttpRequestException(
                $"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase})"); ;

            throw response.StatusCode switch
            {
                HttpStatusCode.BadRequest => new AzureSignalRInvalidArgumentException(response.RequestMessage.RequestUri.ToString(), innerException, detail),
                HttpStatusCode.Unauthorized => new AzureSignalRUnauthorizedException(response.RequestMessage.RequestUri.ToString(), innerException),
                HttpStatusCode.NotFound => new AzureSignalRInaccessibleEndpointException(response.RequestMessage.RequestUri.ToString(), innerException),
                _ => new AzureSignalRRuntimeException(response.RequestMessage.RequestUri.ToString(), innerException),
            };
        }

        private static Uri GetUri(string url, IDictionary<string, StringValues> query)
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
                    sb.Append(Uri.EscapeDataString(value));
                }
            }
            builder.Query = sb.ToString();
            return builder.Uri;
        }

        private HttpRequestMessage BuildRequest(RestApiEndpoint api, HttpMethod httpMethod, string productInfo, string methodName = null, object[] args = null)
        {
            var payload = httpMethod == HttpMethod.Post ? new PayloadMessage { Target = methodName, Arguments = args } : null;
            return GenerateHttpRequest(api.Audience, api.Query, httpMethod, payload, api.Token, productInfo);
        }

        private HttpRequestMessage GenerateHttpRequest(string url, IDictionary<string, StringValues> query, HttpMethod httpMethod, PayloadMessage payload, string tokenString, string productInfo)
        {
            var request = new HttpRequestMessage(httpMethod, GetUri(url, query));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenString);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add(Constants.AsrsUserAgent, productInfo);
            request.Content = new StringContent(JsonConvert.SerializeObject(payload, JsonSerializerSettings), Encoding.UTF8, "application/json");
            return request;
        }
    }
}
