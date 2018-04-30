// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Newtonsoft.Json;

namespace Microsoft.Azure.SignalR
{
    internal class PayloadMessage
    {
        [JsonProperty("x")]
        public IReadOnlyList<string> ExcludedList { get; set; }

        [JsonProperty("p")]
        public IDictionary<string, string> Payloads { get; set; }
    }

    internal class HubMessageSender : IHubMessageSender
    {
        private static readonly TimeSpan DefaultAccessTokenLifetime = TimeSpan.FromSeconds(30);

        private readonly IHubProtocolResolver _hubProtocolResolver;
        private readonly IServiceEndpointUtility _endpointUtility;
        private readonly string _baseUrl;
        private readonly HttpClient _httpClient = new HttpClient();

        public HubMessageSender(IServiceEndpointUtility endpointUtility, IHubProtocolResolver hubProtocolResolver)
        {
            _endpointUtility = endpointUtility ?? throw new ArgumentNullException(nameof(endpointUtility));
            _hubProtocolResolver = hubProtocolResolver ?? throw new ArgumentNullException(nameof(hubProtocolResolver));
            _baseUrl = $"{_endpointUtility.Endpoint}:{ProxyConstants.Port}/api/{ProxyConstants.ApiVersion}";
        }

        public Task<HttpResponseMessage> SendAsync(string path, string method, object[] args,
            IReadOnlyList<string> excludedIds)
        {
            var request = CreateHttpRequestMessage(HttpMethod.Post, path);
            var body = GetRequestBody(method, args, excludedIds);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            return _httpClient.SendAsync(request);
        }

        public Task<HttpResponseMessage> SendAsync(string path, HttpMethod method)
        {
            var request = CreateHttpRequestMessage(method, path);
            return _httpClient.SendAsync(request);
        }

        private HttpRequestMessage CreateHttpRequestMessage(HttpMethod method, string path)
        {
            var url = _baseUrl + path;
            var request = new HttpRequestMessage
            {
                Method = method,
                RequestUri = new Uri(url)
            };

            var bearer = GenerateAccessToken(url, _endpointUtility.AccessKey);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.AcceptCharset.Clear();
            request.Headers.AcceptCharset.Add(new StringWithQualityHeaderValue("UTF-8"));

            return request;
        }

        private string GetRequestBody(string method, object[] args, IReadOnlyList<string> excludedList)
        {
            var message = new InvocationMessage(method, args);
            var payloadMessage = new PayloadMessage();

            if (_hubProtocolResolver.AllProtocols?.Count > 0)
            {
                var payloads = new Dictionary<string, string>(_hubProtocolResolver.AllProtocols.Count);
                foreach (var hubProtocol in _hubProtocolResolver.AllProtocols)
                {
                    // TODO: Get rid of `ToArray`
                    payloads.Add(hubProtocol.Name,
                        Convert.ToBase64String(hubProtocol.GetMessageBytes(message).ToArray()));
                }

                payloadMessage.Payloads = payloads;
            }

            if (excludedList?.Count > 0)
            {
                payloadMessage.ExcludedList = excludedList;
            }

            return JsonConvert.SerializeObject(payloadMessage);
        }

        public static string GenerateAccessToken(string url, string accessKey)
        {
            return AuthenticationHelper.GenerateJwtBearer(
                audience: url,
                claims: null,
                expires: DateTime.UtcNow.Add(DefaultAccessTokenLifetime),
                signingKey: accessKey
            );
        }
    }
}
