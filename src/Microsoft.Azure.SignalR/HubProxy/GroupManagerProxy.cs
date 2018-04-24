// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    internal class GroupManagerProxy : IGroupManager
    {
        private readonly string _baseUri;
        private readonly IHubMessageSender _hubMessageSender;
        private readonly string _accessKey;

        public GroupManagerProxy(IHubMessageSender hubMessageSender, string endpoint, string accessKey, string hubName)
        {
            _baseUri = $"{endpoint}:{ProxyConstants.Port}/api/{ProxyConstants.ApiVersion}/hub/{hubName.ToLower()}/group";
            _hubMessageSender = hubMessageSender;
            _accessKey = accessKey;
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
            var uri = $"{_baseUri}/{groupName}/connection/{connectionId}";
            return _hubMessageSender.SendAsync(uri, GenerateAccessToken(uri), method);
        }

        private string GenerateAccessToken(string audience)
        {
            return AuthenticationHelper.GenerateJwtBearer(
                audience: audience,
                claims: null,
                expires: DateTime.UtcNow.Add(ServiceEndpointUtility.DefaultAccessTokenLifetime),
                signingKey: _accessKey
            );
        }
    }
}
