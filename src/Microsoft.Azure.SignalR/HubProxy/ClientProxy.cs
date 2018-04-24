// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    internal class ClientProxy : IClientProxy
    {
        private readonly string _url;
        private readonly Func<string> _accessTokenProvider;
        private readonly IReadOnlyList<string> _excludedList;
        private readonly IHubMessageSender _hubMessageSender;

        public ClientProxy(IHubMessageSender hubMessageSender, string url, Func<string> accessTokenProvider,
            IReadOnlyList<string> excludedList)
        {
            _hubMessageSender = hubMessageSender;
            _url = url;
            _accessTokenProvider = accessTokenProvider;
            _excludedList = excludedList;
        }

        public Task SendCoreAsync(string method, object[] args, CancellationToken cancellationToken = default)
        {
            return _hubMessageSender.SendAsync(_url, _accessTokenProvider.Invoke(), method, args, _excludedList);
        }
    }
}
