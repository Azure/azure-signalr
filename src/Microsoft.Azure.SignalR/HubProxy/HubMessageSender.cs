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
        private readonly IHubProtocolResolver _hubProtocolResolver;
        private readonly HttpClient _httpClient = new HttpClient();

        public HubMessageSender(IHubProtocolResolver hubProtocolResolver)
        {
            _hubProtocolResolver = hubProtocolResolver;
        }

        public Task<HttpResponseMessage> SendAsync(string url, string bearer, string method, object[] args,
            IReadOnlyList<string> excludedIds)
        {
            var request = CreateHttpRequestMessage(HttpMethod.Post, url, bearer);
            var invocationMessage = CreateInvocationMessage(method, args);
            var payloadMessage = CreatePayloadMessage(invocationMessage, excludedIds);

            // TODO: need more efficient way to send binary to service
            request.Content = new StringContent(JsonConvert.SerializeObject(payloadMessage), Encoding.UTF8,
                "application/json");
            return _httpClient.SendAsync(request);
        }

        public Task<HttpResponseMessage> SendAsync(string url, string bearer, HttpMethod method)
        {
            var request = CreateHttpRequestMessage(method, url, bearer);
            return _httpClient.SendAsync(request);
        }

        private HttpRequestMessage CreateHttpRequestMessage(HttpMethod method, string url, string bearer)
        {
            var request = new HttpRequestMessage
            {
                Method = method,
                RequestUri = new Uri(url)
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.AcceptCharset.Clear();
            request.Headers.AcceptCharset.Add(new StringWithQualityHeaderValue("UTF-8"));

            return request;
        }

        private InvocationMessage CreateInvocationMessage(string methodName, object[] args)
        {
            return new InvocationMessage(methodName, args);
        }

        private PayloadMessage CreatePayloadMessage(HubMessage message, IReadOnlyList<string> excludedList)
        {
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

            return payloadMessage;
        }
    }
}
