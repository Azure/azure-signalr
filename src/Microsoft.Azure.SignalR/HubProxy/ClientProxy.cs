// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Microsoft.Azure.SignalR
{
    public class ClientProxy : IClientProxy
    {
        private readonly string _url;
        private readonly Func<string> _jwtBearerProvider;
        private readonly IReadOnlyList<string> _excludedIds;

        public ClientProxy(string url, Func<string> jwtBearerProvider, IReadOnlyList<string> excludedIds = null)
        {
            _url = url;
            _jwtBearerProvider = jwtBearerProvider;
            _excludedIds = excludedIds;
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

            request.Content = new StringContent(
                JsonConvert.SerializeObject(new
                {
                    method = method,
                    arguments = args,
                    excluded = _excludedIds
                }), Encoding.UTF8, "application/json");

            var client = new HttpClient ();
            return client.SendAsync(request);
        }
    }
}
