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

using Azure.Core.Serialization;

using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Extensions.Primitives;

#nullable enable

namespace Microsoft.Azure.SignalR;

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

    public Task SendAsync(
        RestApiEndpoint api,
        HttpMethod httpMethod,
        CancellationToken cancellationToken = default) =>
        SendAsync(api, httpMethod, (Func<HttpResponseMessage, Task<bool>>?)null, cancellationToken);

    public Task SendAsync(
        RestApiEndpoint api,
        HttpMethod httpMethod,
        Func<HttpResponseMessage, bool>? handleExpectedResponse,
        CancellationToken cancellationToken = default) =>
        SendAsync(api, httpMethod, AsAsync(handleExpectedResponse), cancellationToken);

    public Task SendAsync(
        RestApiEndpoint api,
        HttpMethod httpMethod,
        Func<HttpResponseMessage, Task<bool>>? handleExpectedResponseAsync,
        CancellationToken cancellationToken = default)
    {
        return SendAsyncCore(Constants.HttpClientNames.UserDefault, api, httpMethod, null, null, handleExpectedResponseAsync, null, cancellationToken);
    }

    public Task SendWithRetryAsync(
        RestApiEndpoint api,
        HttpMethod httpMethod,
        Func<HttpResponseMessage, bool>? handleExpectedResponse = null,
        CancellationToken cancellationToken = default)
    {
        return SendWithRetryAsync(api, httpMethod, AsAsync(handleExpectedResponse), cancellationToken);
    }

    public Task SendWithRetryAsync(
        RestApiEndpoint api,
        HttpMethod httpMethod,
        Func<HttpResponseMessage, Task<bool>>? handleExpectedResponseAsync = null,
        CancellationToken cancellationToken = default)
    {
        return SendAsyncCore(Constants.HttpClientNames.Resilient, api, httpMethod, null, null, handleExpectedResponseAsync, null, cancellationToken);
    }

    public Task SendMessageWithRetryAsync(
        RestApiEndpoint api,
        HttpMethod httpMethod,
        string methodName,
        object?[] args,
        Func<HttpResponseMessage, Task<bool>>? handleExpectedResponse = null,
        CancellationToken cancellationToken = default)
    {
        return SendAsyncCore(Constants.HttpClientNames.MessageResilient, api, httpMethod, new InvocationMessage(methodName, args), null, handleExpectedResponse, null, cancellationToken);
    }

    public Task SendMessageWithRetryAsync(
        RestApiEndpoint api,
        HttpMethod httpMethod,
        string methodName,
        object?[] args,
        Func<HttpResponseMessage, Task<bool>>? handleExpectedResponse = null,
        MediaTypeWithQualityHeaderValue? accepts = null,
        CancellationToken cancellationToken = default)
    {
        return SendAsyncCore(Constants.HttpClientNames.MessageResilient, api, httpMethod, new InvocationMessage(methodName, args), null, handleExpectedResponse, accepts, cancellationToken);
    }

    public Task SendStreamMessageWithRetryAsync(
        RestApiEndpoint api,
        HttpMethod httpMethod,
        string streamId,
        object? arg = null,
        Type? typeHint = null,
        Func<HttpResponseMessage, bool>? handleExpectedResponse = null,
        CancellationToken cancellationToken = default)
    {
        return SendAsyncCore(Constants.HttpClientNames.MessageResilient, api, httpMethod, new StreamItemMessage(streamId, arg), typeHint, AsAsync(handleExpectedResponse), null, cancellationToken);
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

    private static async Task ThrowExceptionOnResponseFailureAsync(HttpRequestMessage request, HttpResponseMessage response)
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

        var requestUri = request.RequestUri?.ToString();
        var jwtToken = request.Headers.Authorization?.Parameter ?? null;
        throw response.StatusCode switch
        {
            HttpStatusCode.BadRequest => new AzureSignalRInvalidArgumentException(requestUri, innerException, detail),
            HttpStatusCode.Unauthorized => new AzureSignalRUnauthorizedException(requestUri, innerException, jwtToken),
            HttpStatusCode.NotFound => new AzureSignalRInaccessibleEndpointException(requestUri, innerException),
            _ => new AzureSignalRRuntimeException(response.RequestMessage?.RequestUri?.ToString(), innerException, response.StatusCode, detail),
        };
    }

    private async Task SendAsyncCore(
        string httpClientName,
        RestApiEndpoint api,
        HttpMethod httpMethod,
        HubMessage? body,
        Type? typeHint,
        Func<HttpResponseMessage, Task<bool>>? handleExpectedResponseAsync = null,
        MediaTypeWithQualityHeaderValue? accepts = null,
        CancellationToken cancellationToken = default)
    {
        using var httpClient = _httpClientFactory.CreateClient(httpClientName);
        using var request = BuildRequest(api, httpMethod, body, typeHint);
        if (accepts != null)
        {
            request.Headers.Accept.Add(accepts);
        }
        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (handleExpectedResponseAsync == null)
            {
                await ThrowExceptionOnResponseFailureAsync(request, response);
            }
            else
            {
                if (!await handleExpectedResponseAsync(response))
                {
                    await ThrowExceptionOnResponseFailureAsync(request, response);
                }
            }
        }
        catch (HttpRequestException ex)
        {
            throw new AzureSignalRException($"An error happened when making request to {request.RequestUri}", ex);
        }
    }

    private HttpRequestMessage BuildRequest(RestApiEndpoint api, HttpMethod httpMethod, HubMessage? body, Type? typeHint)
    {
        return GenerateHttpRequest(api.Audience, api.Query, httpMethod, body, typeHint);
    }

    private HttpRequestMessage GenerateHttpRequest(string url, IDictionary<string, StringValues>? query, HttpMethod httpMethod, HubMessage? body, Type? typeHint)
    {
        var request = new HttpRequestMessage(httpMethod, GetUri(url, query));
        request.Content = _payloadContentBuilder.Build(body, typeHint);
        return request;
    }

    private static Func<HttpResponseMessage, Task<bool>>? AsAsync(Func<HttpResponseMessage, bool>? syncFunc) =>
        syncFunc == null ? null : (response => Task.FromResult(syncFunc(response)));
}
