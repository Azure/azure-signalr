// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR.Internal.Protocol;
using Newtonsoft.Json;

namespace Microsoft.Azure.SignalR
{
    public static class HubInvocationMessageExtension
    {
        private const string Separator = ",";
        internal const string ActionKeyName = "action";
        internal const string ConnectionIdKeyName = "connId";
        internal const string ConnectionIdsKeyName = "connIds";
        internal const string UserKeyName = "user";
        internal const string UsersKeyName = "users";
        internal const string GroupNameKeyName = "group";
        internal const string GroupNamesKeyName = "groups";
        internal const string ExcludedIdsKeyName = "excluded";
        internal const string ClaimsKeyName = "claims";

        public static TMessage AddHeader<TMessage>(this TMessage message, string key, string value)
            where TMessage : HubInvocationMessage
        {
            if (message != null && !string.IsNullOrEmpty(key))
            {
                message.Headers.Add(key, value);
            }
            return message;
        }

        public static bool TryGetHeader<TMessage>(this TMessage message, string headerName, out string headerValue)
            where TMessage : HubInvocationMessage
        {
            if (message.Headers != null &&
                message.Headers.TryGetValue(headerName, out headerValue))
            {
                return true;
            }
            headerValue = null;
            return false;
        }

        public static TMessage AddConnectionId<TMessage>(this TMessage message, string connectionId)
            where TMessage : HubInvocationMessage
        {
            return string.IsNullOrEmpty(connectionId) ? message : message.AddHeader(ConnectionIdKeyName, connectionId);
        }

        public static string GetConnectionId<TMessage>(this TMessage message) where TMessage : HubInvocationMessage
        {
            message.Headers.TryGetValue(ConnectionIdKeyName, out var connectionId);
            return connectionId;
        }

        public static TMessage AddConnectionIds<TMessage>(this TMessage message, IReadOnlyList<string> connectionIds)
            where TMessage : HubInvocationMessage
        {
            return message.AddHeader(ConnectionIdsKeyName, string.Join(Separator, connectionIds));
        }

        public static bool TryGetConnectionId<TMessage>(this TMessage message, out string connectionId)
            where TMessage : HubInvocationMessage
        {
            return message.TryGetHeader(ConnectionIdKeyName, out connectionId);
        }

        public static TMessage AddUser<TMessage>(this TMessage message, string userId)
            where TMessage : HubInvocationMessage
        {
            return string.IsNullOrEmpty(userId) ? message : message.AddHeader(UserKeyName, userId);
        }

        public static TMessage AddUsers<TMessage>(this TMessage message, IReadOnlyList<string> userIds)
            where TMessage : HubInvocationMessage
        {
            return userIds != null && userIds.Any() ? message.AddHeader(UsersKeyName, string.Join(Separator, userIds)) : message;
        }

        public static TMessage AddGroupName<TMessage>(this TMessage message, string groupName)
            where TMessage : HubInvocationMessage
        {
            return  string.IsNullOrEmpty(groupName) ? message : message.AddHeader(GroupNameKeyName, groupName);
        }

        public static TMessage AddGroupNames<TMessage>(this TMessage message, IReadOnlyList<string> groupNames)
            where TMessage : HubInvocationMessage
        {
            return groupNames != null && groupNames.Any() ? message.AddHeader(GroupNamesKeyName, string.Join(Separator, groupNames)) : message;
        }

        public static TMessage AddExcludedIds<TMessage>(this TMessage message, IReadOnlyList<string> excludedIds)
            where TMessage : HubInvocationMessage
        {
            return excludedIds != null && excludedIds.Any() ? message.AddHeader(ExcludedIdsKeyName, string.Join(Separator, excludedIds)) : message;
        }

        public static TMessage AddAction<TMessage>(this TMessage message, string actionName)
            where TMessage : HubInvocationMessage
        {
            return string.IsNullOrEmpty(actionName) ? message : message.AddHeader(ActionKeyName, actionName);
        }

        public static bool TryGetClaims<TMessage>(this TMessage message, out IEnumerable<Claim> claims)
            where TMessage : HubInvocationMessage
        {
            claims = message.TryGetHeader(ClaimsKeyName, out var serializedClaims)
                ? JsonConvert.DeserializeObject<IEnumerable<ClaimEntry>>(serializedClaims).Select(x => x.ToClaim())
                : null;

            return claims != null;
        }

        internal class ClaimEntry
        {
            [JsonProperty("t")]
            public string Type { get; set; }

            [JsonProperty("v")]
            public string Value { get; set; }

            public static ClaimEntry FromClaim(Claim claim)
            {
                return new ClaimEntry
                {
                    Type = claim.Type,
                    Value = claim.Value
                };
            }

            public Claim ToClaim()
            {
                return new Claim(Type, Value);
            }
        }
    }
}
