// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.AspNetCore.SignalR.Internal.Protocol;
using Newtonsoft.Json;

namespace Microsoft.Azure.SignalR
{
    internal class HubMessagePayload
    {
        public string ProtocolName { get; set; }

        public string Payload { get; set; }
    }

    internal class HttpMessage
    {
        public int PayloadCount { get; set; }

        public IReadOnlyList<HubMessagePayload> Payloads { get; set; }

        public IReadOnlyList<string> ExcludedIds { get; set; }
    }

    internal class HubMessageSender : IHubMessageSender
    {
        private IHubProtocolResolver _hubProtocolResolver;
        private HttpClient _httpClient = new HttpClient();

        public HubMessageSender(IHubProtocolResolver hubProtocolResolver)
        {
            _hubProtocolResolver = hubProtocolResolver;
        }

        public Task<HttpResponseMessage> PostAsync(string url, string bearer, string method, object[] args, IReadOnlyList<string> excludedIds)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(url)
            };

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", bearer);

            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.AcceptCharset.Clear();
            request.Headers.AcceptCharset.Add(new StringWithQualityHeaderValue("UTF-8"));
            var invocationMessage = CreateInvocationMessage(method, args);
            var httpMessage = new HttpMessage();
            httpMessage.PayloadCount = _hubProtocolResolver.AllProtocols.Count;
            httpMessage.ExcludedIds = excludedIds;
            foreach (var hubProtocol in _hubProtocolResolver.AllProtocols)
            {
                var payloads = new HubMessagePayload();
                payloads.ProtocolName = hubProtocol.Name;
                payloads.Payload = Convert.ToBase64String(hubProtocol.WriteToArray(invocationMessage));
            }

            request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(httpMessage)));
            return _httpClient.SendAsync(request);
        }

        public Task<HttpResponseMessage> SendAsync(string url, string bearer, HttpMethod method)
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
            return _httpClient.SendAsync(request);
        }

        private InvocationMessage CreateInvocationMessage(string methodName, object[] args)
        {
            return new InvocationMessage(target: methodName, argumentBindingException: null, arguments: args);
        }
    }
}
