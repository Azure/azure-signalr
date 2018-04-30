// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    internal class GroupManagerProxy : IGroupManager
    {
        private readonly IHubMessageSender _hubMessageSender;
        private readonly string _hubName;

        public GroupManagerProxy(IHubMessageSender hubMessageSender, string hubName)
        {
            _hubMessageSender = hubMessageSender;
            _hubName = hubName;
        }

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            return InternalSendAsync(connectionId, groupName, HttpMethod.Post);
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            return InternalSendAsync(connectionId, groupName, HttpMethod.Delete);
        }

        private Task InternalSendAsync(string connectionId, string groupName, HttpMethod method)
        {
            var path = $"/hub/{_hubName}/group/{groupName}/connection/{connectionId}";
            return _hubMessageSender.SendAsync(path, method);
        }
    }
}
