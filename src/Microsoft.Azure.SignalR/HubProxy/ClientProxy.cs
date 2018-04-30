// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    internal class ClientProxy : IClientProxy
    {
        private readonly string _path;
        private readonly IReadOnlyList<string> _excludedList;
        private readonly IHubMessageSender _hubMessageSender;

        public ClientProxy(IHubMessageSender hubMessageSender, string path, IReadOnlyList<string> excludedList = null)
        {
            _hubMessageSender = hubMessageSender;
            _path = path;
            _excludedList = excludedList;
        }

        public Task SendCoreAsync(string method, object[] args, CancellationToken cancellationToken = default)
        {
            return _hubMessageSender.SendAsync(_path, method, args, _excludedList);
        }
    }
}
