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
        private IHubMessageSender _hubMessageSender;
        public ClientProxy(IHubMessageSender hubMessageSender, string url, Func<string> jwtBearerProvider, IReadOnlyList<string> excludedIds = null)
        {
            _hubMessageSender = hubMessageSender;
            _url = url;
            _jwtBearerProvider = jwtBearerProvider;
            _excludedIds = excludedIds;
        }

        // TODO: Translate HttpResponseMessage to typed error
        public Task<HttpResponseMessage> SendAsync(string method, object[] args)
        {
            return _hubMessageSender.PostAsync(_url, _jwtBearerProvider.Invoke(), method, args);
        }
    }
}
