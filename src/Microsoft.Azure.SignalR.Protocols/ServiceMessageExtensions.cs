// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using Newtonsoft.Json;

namespace Microsoft.Azure.SignalR
{  
    public static class ServiceMessageExtension
    {
        // All public static methods are listed in method names' alphabetic order.
        public static TMessage AddAction<TMessage>(this TMessage message, string actionName)
            where TMessage : ServiceMessage
        {
            return message.AddOrUpdateMetadata(ServiceMessage.ActionKeyName, actionName);
        }

        public static TMessage AddClaims<TMessage>(this TMessage message, IEnumerable<Claim> claims)
            where TMessage : ServiceMessage
        {
            return message.AddOrUpdateMetadata(ServiceMessage.ClaimsKeyName,
                JsonConvert.SerializeObject(claims.Select(ClaimEntry.FromClaim)));
        }

        public static TMessage AddConnectionId<TMessage>(this TMessage message, string connectionId)
            where TMessage : ServiceMessage
        {
            return message.AddOrUpdateMetadata(ServiceMessage.ConnectionIdKeyName, connectionId);
        }

        public static TMessage AddExcludedIds<TMessage>(this TMessage message, IReadOnlyList<string> excludedIds)
            where TMessage : ServiceMessage
        {
            return message.AddOrUpdateMetadata(ServiceMessage.ExcludedIdsKeyName, string.Join(",", excludedIds));
        }

        public static TMessage AddGroupName<TMessage>(this TMessage message, string groupName)
            where TMessage : ServiceMessage
        {
            return message.AddOrUpdateMetadata(ServiceMessage.GroupNameKeyName, groupName);
        }

        public static TMessage AddMetadata<TMessage>(this TMessage message, IDictionary<string, string> metadata)
            where TMessage : ServiceMessage
        {
            if (message == null || metadata == null) return message;
            foreach (var kvp in metadata)
            {
                message.Headers.Add(kvp.Key, kvp.Value);
            }
            return message;
        }

        public static TMessage AddOrUpdateMetadata<TMessage>(this TMessage message, string key, string value)
            where TMessage : ServiceMessage
        {
            if (message != null && !string.IsNullOrEmpty(key))
            {
                if (message.Headers.ContainsKey(key))
                {
                    message.Headers[key] = value;
                }
                else
                {
                    message.Headers.Add(key, value);
                }
            }
            return message;
        }

        public static TMessage AddTimestamp<TMessage>(this TMessage message)
            where TMessage : ServiceMessage
        {
            return message.AddOrUpdateMetadata(ServiceMessage.TimestampKeyName, Stopwatch.GetTimestamp().ToString());
        }

        public static string GetConnectionId<TMessage>(this TMessage message) where TMessage : ServiceMessage
        {
            message.Headers.TryGetValue(ServiceMessage.ConnectionIdKeyName, out var connectionId);
            return connectionId;
        }

        public static long? GetMessageDelay<TMessage>(this TMessage message)
            where TMessage : ServiceMessage
        {
            if (message.Headers.TryGetValue(ServiceMessage.TimestampKeyName, out var startTimestampString) &&
                long.TryParse(startTimestampString, out var startTimestamp))
            {
                return Stopwatch.GetTimestamp() - startTimestamp;
            }
            return null;
        }

        public static bool TryGetAction<TMessage>(this TMessage message, out string actionName)
            where TMessage : ServiceMessage
        {
            return message.TryGetMetadata(ServiceMessage.ActionKeyName, out actionName);
        }

        public static bool TryGetClaims<TMessage>(this TMessage message, out IEnumerable<Claim> claims)
            where TMessage : ServiceMessage
        {
            claims = message.TryGetMetadata(ServiceMessage.ClaimsKeyName, out var serializedClaims)
                ? JsonConvert.DeserializeObject<IEnumerable<ClaimEntry>>(serializedClaims).Select(x => x.ToClaim())
                : null;

            return claims != null;
        }

        public static bool TryGetConnectionId<TMessage>(this TMessage message, out string connectionId)
            where TMessage : ServiceMessage
        {
            return message.TryGetMetadata(ServiceMessage.ConnectionIdKeyName, out connectionId);
        }

        public static bool TryGetConnectionIds<TMessage>(this TMessage message, out IReadOnlyList<string> connectionIds)
            where TMessage : ServiceMessage
        {
            return message.TryGetList(ServiceMessage.ConnectionIdsKeyName, out connectionIds);
        }

        public static bool TryGetExcludedIds<TMessage>(this TMessage message, out IReadOnlyList<string> excludedIdList)
            where TMessage : ServiceMessage
        {
            return TryGetList(message, ServiceMessage.ExcludedIdsKeyName, out excludedIdList);
        }

        public static bool TryGetGroupName<TMessage>(this TMessage message, out string groupName)
            where TMessage : ServiceMessage
        {
            return message.TryGetMetadata(ServiceMessage.GroupNameKeyName, out groupName);
        }

        public static bool TryGetGroupNames<TMessage>(this TMessage message, out IReadOnlyList<string> groupNames)
            where TMessage : ServiceMessage
        {
            return message.TryGetList(ServiceMessage.GroupNamesKeyName, out groupNames);
        }

        public static bool TryGetMetadata<TMessage>(this TMessage message, string metadataName, out string metadataValue)
            where TMessage : ServiceMessage
        {
            if (message.Headers != null &&
                message.Headers.TryGetValue(metadataName, out metadataValue))
            {
                return true;
            }
            metadataValue = null;
            return false;
        }

        public static bool TryGetUser<TMessage>(this TMessage message, out string userId)
            where TMessage : ServiceMessage
        {
            return message.TryGetMetadata(ServiceMessage.UserKeyName, out userId);
        }

        public static bool TryGetUsers<TMessage>(this TMessage message, out IReadOnlyList<string> userIds)
            where TMessage : ServiceMessage
        {
            return message.TryGetList(ServiceMessage.UsersKeyName, out userIds);
        }

        private static bool TryGetList<TMessage>(this TMessage message, string keyName, out IReadOnlyList<string> list)
            where TMessage : ServiceMessage
        {
            list = message.TryGetMetadata(keyName, out var value)
                ? new List<string>(value.Split(','))
                : null;

            return list != null;
        }
    }
}
