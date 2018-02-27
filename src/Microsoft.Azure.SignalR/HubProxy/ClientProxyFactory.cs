// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;

namespace Microsoft.Azure.SignalR
{
    internal class ClientProxyFactory
    {
        public static ClientProxy CreateAllClientsProxy(string endpoint, string apiVersion, string accessKey,
            string hubName)
        {
            return InternalCreateClientProxy(
                GetBaseUrl(endpoint, apiVersion, hubName),
                accessKey, hubName);
        }

        public static ClientProxy CreateAllClientsExceptProxy(string endpoint, string apiVersion, string accessKey,
            string hubName, IReadOnlyList<string> excludedIds)
        {
            return InternalCreateClientProxy(
                GetBaseUrl(endpoint, apiVersion, hubName),
                accessKey, hubName, excludedIds);
        }

        public static ClientProxy CreateSingleClientProxy(string endpoint, string apiVersion, string accessKey,
            string hubName, string connectionId)
        {
            return InternalCreateClientProxy(
                $"{GetBaseUrl(endpoint, apiVersion, hubName)}/connection/{connectionId}",
                accessKey, hubName);
        }

        public static ClientProxy CreateMultipleClientProxy(string endpoint, string apiVersion, string accessKey,
            string hubName, IReadOnlyList<string> connectionIds)
        {
            return InternalCreateClientProxy(
                $"{GetBaseUrl(endpoint, apiVersion, hubName)}/connections/{string.Join(",", connectionIds)}",
                accessKey, hubName);
        }

        public static ClientProxy CreateSingleUserProxy(string endpoint, string apiVersion, string accessKey,
            string hubName, string userId)
        {
            return InternalCreateClientProxy(
                $"{GetBaseUrl(endpoint, apiVersion, hubName)}/user/{userId}",
                accessKey, hubName);
        }

        public static ClientProxy CreateMultipleUserProxy(string endpoint, string apiVersion, string accessKey,
            string hubName, IReadOnlyList<string> userIds)
        {
            return InternalCreateClientProxy(
                $"{GetBaseUrl(endpoint, apiVersion, hubName)}/users/{string.Join(",", userIds)}",
                accessKey, hubName);
        }

        public static ClientProxy CreateSingleGroupProxy(string endpoint, string apiVersion, string accessKey,
            string hubName, string groupName)
        {
            return InternalCreateClientProxy(
                $"{GetBaseUrl(endpoint, apiVersion, hubName)}/group/{groupName}",
                accessKey, hubName);
        }

        public static ClientProxy CreateMultipleGroupProxy(string endpoint, string apiVersion, string accessKey,
            string hubName, IReadOnlyList<string> groupNames)
        {
            return InternalCreateClientProxy(
                $"{GetBaseUrl(endpoint, apiVersion, hubName)}/groups/{string.Join(",", groupNames)}",
                accessKey, hubName);
        }

        public static ClientProxy CreateSingleGroupExceptProxy(string endpoint, string apiVersion, string accessKey,
            string hubName, string groupName, IReadOnlyList<string> excludedIds)
        {
            return InternalCreateClientProxy(
                $"{GetBaseUrl(endpoint, apiVersion, hubName)}/group/{groupName}",
                accessKey, hubName, excludedIds);
        }

        private static string GetBaseUrl(string endpoint, string apiVersion, string hubName)
        {
            return $"{endpoint}/{apiVersion}/hub/{hubName}";
        }

        private static ClientProxy InternalCreateClientProxy(string url, string accessKey, string hubName,
            IReadOnlyList<string> excludedIds = null)
        {
            return new ClientProxy(url, () => GenerateAccessToken(url, accessKey, hubName), excludedIds);
        }

        private static string GenerateAccessToken(string audience, string accessKey, string hubName)
        {
            var name = $"HubProxy[{hubName}]";
            return AuthenticationHelper.GenerateJwtBearer(
                audience: audience,
                claims: new[]
                {
                    new Claim(ClaimTypes.Name, name),
                    new Claim(ClaimTypes.NameIdentifier, name)
                },
                expires: DateTime.UtcNow.Add(TokenProvider.DefaultAccessTokenLifetime),
                signingKey: accessKey
            );
        }
    }
}