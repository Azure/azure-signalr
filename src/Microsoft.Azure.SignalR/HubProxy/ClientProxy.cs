// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    public class ClientProxy : IClientProxy
    {
        private readonly string _url;
        private readonly Func<string> _jwtBearerProvider;
        private readonly IReadOnlyList<string> _excludedIds;
        private IHubMessageSender _hubMessageSender;

        internal const int ProxyPort = 5002;

        internal const string DefaultApiVersion = "v1-preview";

        public ClientProxy(IHubMessageSender hubMessageSender, string url, Func<string> jwtBearerProvider, IReadOnlyList<string> excludedIds = null)
        {
            _hubMessageSender = hubMessageSender;
            _url = url;
            _jwtBearerProvider = jwtBearerProvider;
            _excludedIds = excludedIds;
        }

        // TODO: Translate HttpResponseMessage to typed error
        public Task SendCoreAsync(string method, object[] args)
        {
            return _hubMessageSender.PostAsync(_url, _jwtBearerProvider.Invoke(), method, args);
        }
    }
}
