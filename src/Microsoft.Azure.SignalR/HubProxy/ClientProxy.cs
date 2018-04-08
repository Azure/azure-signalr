// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Internal.Protocol;
using Newtonsoft.Json;

namespace Microsoft.Azure.SignalR
{
    public class ClientProxy : IClientProxy
    {
        private readonly string _url;
        private readonly Func<string> _jwtBearerProvider;
        private readonly IReadOnlyList<string> _excludedIds;
        private readonly IHubProtocol _jsonHubProtocol;
        private readonly IHubProtocol _messagePackHubProtocol;

        public ClientProxy(string url, Func<string> jwtBearerProvider, IReadOnlyList<string> excludedIds = null)
        {
            _url = url;
            _jwtBearerProvider = jwtBearerProvider;
            _excludedIds = excludedIds;
            _jsonHubProtocol = new JsonHubProtocol();
            _messagePackHubProtocol = new MessagePackHubProtocol();
        }

        // TODO: Translate HttpResponseMessage to typed error
        public Task<HttpResponseMessage> SendAsync(string method, object[] args)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(_url)
            };

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _jwtBearerProvider.Invoke());

            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.AcceptCharset.Clear();
            request.Headers.AcceptCharset.Add(new StringWithQualityHeaderValue("UTF-8"));
            var invocationMessage = CreateInvocationMessage(method, args);
            
            request.Content = new StringContent(
                JsonConvert.SerializeObject(new
                {
                    // Binary in JSON must be base64 encoded.
                    jsonPayload = Convert.ToBase64String(_jsonHubProtocol.WriteToArray(invocationMessage)),
                    msgpackPayload = Convert.ToBase64String(_messagePackHubProtocol.WriteToArray(invocationMessage)),
                    excluded = _excludedIds
                }), Encoding.UTF8, "application/json");

            var client = new HttpClient();
            return client.SendAsync(request);
        }

        private InvocationMessage CreateInvocationMessage(string methodName, object[] args)
        {
            return new InvocationMessage(target: methodName, argumentBindingException: null, arguments: args);
        }
    }
}
