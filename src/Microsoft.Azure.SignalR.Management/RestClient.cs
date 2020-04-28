// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common;
using Newtonsoft.Json;

namespace Microsoft.Azure.SignalR.Management
{
    internal class RestClient
    {
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
            HttpResponseMessage response = null;

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

            switch (response.StatusCode)
            {
                case HttpStatusCode.BadRequest:
                    throw new AzureSignalRInvalidArgumentException(response.RequestMessage.RequestUri.ToString(), innerException, detail);
                case HttpStatusCode.Unauthorized:
                    throw new AzureSignalRUnauthorizedException(response.RequestMessage.RequestUri.ToString(), innerException);
                case HttpStatusCode.NotFound:
                    throw new AzureSignalRInaccessibleEndpointException(response.RequestMessage.RequestUri.ToString(), innerException);
                default:
                    throw new AzureSignalRRuntimeException(response.RequestMessage.RequestUri.ToString(), innerException);
            }
        }

        private HttpRequestMessage BuildRequest(RestApiEndpoint api, HttpMethod httpMethod, string productInfo, string methodName = null, object[] args = null)
        {
            var payload = httpMethod == HttpMethod.Post ? new PayloadMessage { Target = methodName, Arguments = args } : null;
            return GenerateHttpRequest(api.Audience, httpMethod, payload, api.Token, productInfo);
        }

        private HttpRequestMessage GenerateHttpRequest(string url, HttpMethod httpMethod, PayloadMessage payload, string tokenString, string productInfo)
        {
            var request = new HttpRequestMessage(httpMethod, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenString);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add(Constants.AsrsUserAgent, productInfo);
            request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            return request;
        }
    }
}
