// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Azure.SignalR
{
    internal class ClientProxyFactory
    {
        public static ClientProxy CreateAllClientsProxy(IHubMessageSender hubMessageSender, string endpoint,
            string accessKey, string hubName)
        {
            return InternalCreateClientProxy(hubMessageSender, GetBaseUrl(endpoint, hubName), accessKey, hubName);
        }

        public static ClientProxy CreateAllClientsExceptProxy(IHubMessageSender hubMessageSender, string endpoint,
            string accessKey, string hubName, IReadOnlyList<string> excludedList)
        {
            return InternalCreateClientProxy(hubMessageSender, GetBaseUrl(endpoint, hubName), accessKey, hubName,
                excludedList);
        }

        public static ClientProxy CreateSingleClientProxy(IHubMessageSender hubMessageSender, string endpoint,
            string accessKey, string hubName, string connectionId)
        {
            var url = $"{GetBaseUrl(endpoint, hubName)}/connection/{connectionId}";
            return InternalCreateClientProxy(hubMessageSender, url, accessKey, hubName);
        }

        public static ClientProxy CreateMultipleClientProxy(IHubMessageSender hubMessageSender, string endpoint,
            string accessKey, string hubName, IReadOnlyList<string> connectionIds)
        {
            var url = $"{GetBaseUrl(endpoint, hubName)}/connections/{string.Join(",", connectionIds)}";
            return InternalCreateClientProxy(hubMessageSender, url, accessKey, hubName);
        }

        public static ClientProxy CreateSingleUserProxy(IHubMessageSender hubMessageSender, string endpoint,
            string accessKey, string hubName, string userId)
        {
            var url = $"{GetBaseUrl(endpoint, hubName)}/user/{userId}";
            return InternalCreateClientProxy(hubMessageSender, url, accessKey, hubName);
        }

        public static ClientProxy CreateMultipleUserProxy(IHubMessageSender hubMessageSender, string endpoint,
            string accessKey, string hubName, IReadOnlyList<string> userIds)
        {
            var url = $"{GetBaseUrl(endpoint, hubName)}/users/{string.Join(",", userIds)}";
            return InternalCreateClientProxy(hubMessageSender, url, accessKey, hubName);
        }

        public static ClientProxy CreateSingleGroupProxy(IHubMessageSender hubMessageSender, string endpoint,
            string accessKey, string hubName, string groupName)
        {
            var url = $"{GetBaseUrl(endpoint, hubName)}/group/{groupName}";
            return InternalCreateClientProxy(hubMessageSender, url, accessKey, hubName);
        }

        public static ClientProxy CreateMultipleGroupProxy(IHubMessageSender hubMessageSender, string endpoint,
            string accessKey, string hubName, IReadOnlyList<string> groupNames)
        {
            var url = $"{GetBaseUrl(endpoint, hubName)}/groups/{string.Join(",", groupNames)}";
            return InternalCreateClientProxy(hubMessageSender, url, accessKey, hubName);
        }

        public static ClientProxy CreateSingleGroupExceptProxy(IHubMessageSender hubMessageSender, string endpoint,
            string accessKey, string hubName, string groupName, IReadOnlyList<string> excludedList)
        {
            var url = $"{GetBaseUrl(endpoint, hubName)}/group/{groupName}";
            return InternalCreateClientProxy(hubMessageSender, url, accessKey, hubName, excludedList);
        }

        public static string GetBaseUrl(string endpoint, string hubName)
        {
            return $"{endpoint}:{ProxyConstants.Port}/api/{ProxyConstants.ApiVersion}/hub/{hubName}";
        }

        private static ClientProxy InternalCreateClientProxy(IHubMessageSender hubMessageSender, string url,
            string accessKey, string hubName, IReadOnlyList<string> excludedList = null)
        {
            return new ClientProxy(hubMessageSender, url, () => GenerateAccessToken(url, accessKey, hubName),
                excludedList);
        }

        public static string GenerateAccessToken(string audience, string accessKey, string hubName)
        {
            return AuthenticationHelper.GenerateJwtBearer(
                audience: audience,
                claims: null,
                expires: DateTime.UtcNow.Add(ServiceEndpointUtility.DefaultAccessTokenLifetime),
                signingKey: accessKey
            );
        }
    }
}
