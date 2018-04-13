// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;

namespace Microsoft.Azure.SignalR
{
    internal class ClientProxyFactory
    {
        public static ClientProxy CreateAllClientsProxy(IHubMessageSender hubMessageSender, string endpoint, string accessKey,
            string hubName)
        {
            return InternalCreateClientProxy(hubMessageSender,
                GetBaseUrl(endpoint, hubName),
                accessKey, hubName);
        }

        public static ClientProxy CreateAllClientsExceptProxy(IHubMessageSender hubMessageSender, string endpoint, string accessKey,
            string hubName, IReadOnlyList<string> excludedIds)
        {
            return InternalCreateClientProxy(hubMessageSender,
                GetBaseUrl(endpoint, hubName),
                accessKey, hubName, excludedIds);
        }

        public static ClientProxy CreateSingleClientProxy(IHubMessageSender hubMessageSender, string endpoint, string accessKey,
            string hubName, string connectionId)
        {
            return InternalCreateClientProxy(hubMessageSender,
                $"{GetBaseUrl(endpoint, hubName)}/connection/{connectionId}",
                accessKey, hubName);
        }

        public static ClientProxy CreateMultipleClientProxy(IHubMessageSender hubMessageSender, string endpoint, string accessKey,
            string hubName, IReadOnlyList<string> connectionIds)
        {
            return InternalCreateClientProxy(hubMessageSender,
                $"{GetBaseUrl(endpoint, hubName)}/connections/{string.Join(",", connectionIds)}",
                accessKey, hubName);
        }

        public static ClientProxy CreateSingleUserProxy(IHubMessageSender hubMessageSender, string endpoint, string accessKey,
            string hubName, string userId)
        {
            return InternalCreateClientProxy(hubMessageSender,
                $"{GetBaseUrl(endpoint, hubName)}/user/{userId}",
                accessKey, hubName);
        }

        public static ClientProxy CreateMultipleUserProxy(IHubMessageSender hubMessageSender, string endpoint, string accessKey,
            string hubName, IReadOnlyList<string> userIds)
        {
            return InternalCreateClientProxy(hubMessageSender,
                $"{GetBaseUrl(endpoint, hubName)}/users/{string.Join(",", userIds)}",
                accessKey, hubName);
        }

        public static ClientProxy CreateSingleGroupProxy(IHubMessageSender hubMessageSender, string endpoint, string accessKey,
            string hubName, string groupName)
        {
            return InternalCreateClientProxy(hubMessageSender,
                $"{GetBaseUrl(endpoint, hubName)}/group/{groupName}",
                accessKey, hubName);
        }

        public static ClientProxy CreateMultipleGroupProxy(IHubMessageSender hubMessageSender, string endpoint, string accessKey,
            string hubName, IReadOnlyList<string> groupNames)
        {
            return InternalCreateClientProxy(hubMessageSender,
                $"{GetBaseUrl(endpoint, hubName)}/groups/{string.Join(",", groupNames)}",
                accessKey, hubName);
        }

        public static ClientProxy CreateSingleGroupExceptProxy(IHubMessageSender hubMessageSender, string endpoint, string accessKey,
            string hubName, string groupName, IReadOnlyList<string> excludedIds)
        {
            return InternalCreateClientProxy(hubMessageSender,
                $"{GetBaseUrl(endpoint, hubName)}/group/{groupName}",
                accessKey, hubName, excludedIds);
        }

        public static string GetBaseUrl(string endpoint, string hubName)
        {
            return $"{endpoint}:{ClientProxy.ProxyPort}/{ClientProxy.DefaultApiVersion}/hub/{hubName}";
        }

        private static ClientProxy InternalCreateClientProxy(IHubMessageSender hubMessageSender, string url, string accessKey, string hubName,
            IReadOnlyList<string> excludedIds = null)
        {
            return new ClientProxy(hubMessageSender, url, () => GenerateAccessToken(url, accessKey, hubName), excludedIds);
        }

        public static string GenerateAccessToken(string audience, string accessKey, string hubName)
        {
            var name = $"HubProxy[{hubName}]";
            return AuthenticationHelper.GenerateJwtBearer(
                audience: audience,
                claims: new[]
                {
                    new Claim(ClaimTypes.Name, name),
                    new Claim(ClaimTypes.NameIdentifier, name)
                },
                expires: DateTime.UtcNow.Add(ConnectionServiceProvider.DefaultAccessTokenLifetime),
                signingKey: accessKey
            );
        }
    }
}