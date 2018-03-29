// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Newtonsoft.Json;

namespace Microsoft.Azure.SignalR
{
    public class MessageMetaDataDictionary : Dictionary<string, string>
    {
        // All public static methods are listed in method names' alphabetic order.
        public MessageMetaDataDictionary AddAction(string actionName)
        {
            return AddOrUpdateMetadata(HubInvocationMessageWrapper.ActionKeyName, actionName);
        }

        public MessageMetaDataDictionary AddClaims(IEnumerable<Claim> claims)
        {
            return AddOrUpdateMetadata(HubInvocationMessageWrapper.ClaimsKeyName,
                JsonConvert.SerializeObject(claims.Select(ClaimEntry.FromClaim)));
        }

        public MessageMetaDataDictionary AddConnectionId(string connectionId)
        {
            return AddOrUpdateMetadata(HubInvocationMessageWrapper.ConnectionIdKeyName, connectionId);
        }

        public MessageMetaDataDictionary AddConnectionIds(IReadOnlyList<string> connectionIds)
        {
            return AddOrUpdateMetadata(HubInvocationMessageWrapper.ConnectionIdsKeyName, string.Join(",", connectionIds));
        }

        public MessageMetaDataDictionary AddExcludedIds(IReadOnlyList<string> excludedIds)
        {
            return AddOrUpdateMetadata(HubInvocationMessageWrapper.ExcludedIdsKeyName, string.Join(",", excludedIds));
        }

        public MessageMetaDataDictionary AddGroupName(string groupName)
        {
            return AddOrUpdateMetadata(HubInvocationMessageWrapper.GroupNameKeyName, groupName);
        }

        public MessageMetaDataDictionary AddGroupsName(IReadOnlyList<string> groupsName)
        {
            return AddOrUpdateMetadata(HubInvocationMessageWrapper.GroupNamesKeyName, string.Join(",", groupsName));
        }

        public MessageMetaDataDictionary AddMetadata(IDictionary<string, string> dic)
        {
            if (dic == null) return this;
            foreach (var kvp in dic)
            {
                this.Add(kvp.Key, kvp.Value);
            }
            return this;
        }
        public MessageMetaDataDictionary AddMetadata(MessageMetaDataDictionary meta)
        {
            if (meta == null) return this;
            foreach (var kvp in meta)
            {
                Add(kvp.Key, kvp.Value);
            }
            return this;
        }

        public MessageMetaDataDictionary AddOrUpdateMetadata(string key, string value)
        {
            if (ContainsKey(key))
            {
                this[key] = value;
            }
            else
            {
                Add(key, value);
            }
            return this;
        }

        public MessageMetaDataDictionary AddUserId(string userId)
        {
            return AddOrUpdateMetadata(HubInvocationMessageWrapper.UserKeyName, userId);
        }

        public MessageMetaDataDictionary AddUserIds(IReadOnlyList<string> userIds)
        {
            return AddOrUpdateMetadata(HubInvocationMessageWrapper.UsersKeyName, string.Join(",", userIds));
        }

        public bool TryGetAction(out string actionName)
        {
            return TryGetMetadata(HubInvocationMessageWrapper.ActionKeyName, out actionName);
        }

        public bool TryGetClaims(out IEnumerable<Claim> claims)
        {
            claims = TryGetMetadata(HubInvocationMessageWrapper.ClaimsKeyName, out var serializedClaims)
                ? JsonConvert.DeserializeObject<IEnumerable<ClaimEntry>>(serializedClaims).Select(x => x.ToClaim())
                : null;

            return claims != null;
        }

        public bool TryGetConnectionIds(out IReadOnlyList<string> connectionIds)
        {
            return TryGetList(HubInvocationMessageWrapper.ConnectionIdsKeyName, out connectionIds);
        }

        public bool TryGetExcludedIds(out IReadOnlyList<string> excludedIdList)
        {
            return TryGetList(HubInvocationMessageWrapper.ExcludedIdsKeyName, out excludedIdList);
        }

        public bool TryGetGroupName(out string groupName)
        {
            return TryGetMetadata(HubInvocationMessageWrapper.GroupNameKeyName, out groupName);
        }

        public bool TryGetGroupsName(out IReadOnlyList<string> groupsName)
        {
            return TryGetList(HubInvocationMessageWrapper.GroupNamesKeyName, out groupsName);
        }

        public bool TryGetMetadata(string metadataName, out string metadataValue)
        {
            if (TryGetValue(metadataName, out metadataValue))
            {
                return true;
            }
            metadataValue = null;
            return false;
        }

        public bool TryGetUserIds(out IReadOnlyList<string> userIdsList)
        {
            return TryGetList(HubInvocationMessageWrapper.UsersKeyName, out userIdsList);
        }

        public bool TryGetUserId(out string userId)
        {
            return TryGetMetadata(HubInvocationMessageWrapper.UserKeyName, out userId);
        }

        private bool TryGetList(string key, out IReadOnlyList<string> list)
        {
            list = TryGetMetadata(key, out var value)
                ? new List<string>(value.Split(','))
                : null;
            return list != null;
        }
    }
}
