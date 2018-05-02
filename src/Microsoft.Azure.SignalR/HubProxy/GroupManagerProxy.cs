// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    internal class GroupManagerProxy : IGroupManager
    {
        private readonly IHubMessageSender _hubMessageSender;
        private readonly string _encodedHubName;

        public GroupManagerProxy(IHubMessageSender hubMessageSender, string hubName)
        {
            CheckNullString(hubName, nameof(hubName));

            _encodedHubName = WebUtility.UrlEncode(hubName);
            _hubMessageSender = hubMessageSender ?? throw new ArgumentNullException(nameof(hubMessageSender));
            
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
            CheckNullString(connectionId, nameof(connectionId));
            CheckNullString(groupName, nameof(groupName));

            var encodedGroupName = WebUtility.UrlEncode(groupName);
            var encodedConnectionId = WebUtility.UrlEncode(connectionId);
            var path = $"/hub/{_encodedHubName}/group/{encodedGroupName}/connection/{encodedConnectionId}";
            return _hubMessageSender.SendAsync(path, method);
        }

        private void CheckNullString(string value, string name)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentNullException(name);
            }
        }
    }
}
