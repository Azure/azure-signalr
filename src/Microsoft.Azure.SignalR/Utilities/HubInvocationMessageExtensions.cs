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
        internal const string ActionKeyName = "action";
        internal const string ConnectionIdKeyName = "connId";
        internal const string GroupNameKeyName = "group";
        internal const string ExcludedIdsKeyName = "excluded";
        internal const string ClaimsKeyName = "claims";

        public static TMessage AddMetadata<TMessage>(this TMessage message, IDictionary<string, string> metadata)
            where TMessage : HubInvocationMessage
        {
            if (message == null || metadata == null) return message;
            foreach (var kvp in metadata)
            {
                message.Headers.Add(kvp.Key, kvp.Value);
            }
            return message;
        }

        public static TMessage AddMetadata<TMessage>(this TMessage message, string key, string value)
            where TMessage : HubInvocationMessage
        {
            if (message != null && !string.IsNullOrEmpty(key))
            {
                message.Headers.Add(key, value);
            }
            return message;
        }

        public static bool TryGetMetadata<TMessage>(this TMessage message, string metadataName,
            out string metadataValue)
            where TMessage : HubInvocationMessage
        {
            if (message.Headers != null &&
                message.Headers.TryGetValue(metadataName, out metadataValue))
            {
                return true;
            }
            metadataValue = null;
            return false;
        }

        public static TMessage AddConnectionId<TMessage>(this TMessage message, string connectionId)
            where TMessage : HubInvocationMessage
        {
            return message.AddMetadata(ConnectionIdKeyName, connectionId);
        }

        public static string GetConnectionId<TMessage>(this TMessage message) where TMessage : HubInvocationMessage
        {
            message.Headers.TryGetValue(ConnectionIdKeyName, out var connectionId);
            return connectionId;
        }

        public static bool TryGetConnectionId<TMessage>(this TMessage message, out string connectionId)
            where TMessage : HubInvocationMessage
        {
            return message.TryGetMetadata(ConnectionIdKeyName, out connectionId);
        }

        public static TMessage AddGroupName<TMessage>(this TMessage message, string groupName)
            where TMessage : HubInvocationMessage
        {
            return message.AddMetadata(GroupNameKeyName, groupName);
        }

        public static bool TryGetGroupName<TMessage>(this TMessage message, out string groupName)
            where TMessage : HubInvocationMessage
        {
            return message.TryGetMetadata(GroupNameKeyName, out groupName);
        }

        public static TMessage AddExcludedIds<TMessage>(this TMessage message, IReadOnlyList<string> excludedIds)
            where TMessage : HubInvocationMessage
        {
            return message.AddMetadata(ExcludedIdsKeyName, string.Join(",", excludedIds));
        }

        public static bool TryGetExcludedIds<TMessage>(this TMessage message, out IReadOnlyList<string> excludedIdList)
            where TMessage : HubInvocationMessage
        {
            excludedIdList = message.TryGetMetadata(ExcludedIdsKeyName, out var value)
                ? new List<string>(value.Split(','))
                : null;

            return excludedIdList != null;
        }

        public static TMessage AddAction<TMessage>(this TMessage message, string actionName)
            where TMessage : HubInvocationMessage
        {
            return message.AddMetadata(ActionKeyName, actionName);
        }

        public static bool TryGetAction<TMessage>(this TMessage message, out string actionName)
            where TMessage : HubInvocationMessage
        {
            return message.TryGetMetadata(ActionKeyName, out actionName);
        }

        public static TMessage AddClaims<TMessage>(this TMessage message, IEnumerable<Claim> claims)
            where TMessage : HubInvocationMessage
        {
            return message.AddMetadata(ClaimsKeyName,
                JsonConvert.SerializeObject(claims.Select(ClaimEntry.FromClaim)));
        }

        public static bool TryGetClaims<TMessage>(this TMessage message, out IEnumerable<Claim> claims)
            where TMessage : HubInvocationMessage
        {
            claims = message.TryGetMetadata(ClaimsKeyName, out var serializedClaims)
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
