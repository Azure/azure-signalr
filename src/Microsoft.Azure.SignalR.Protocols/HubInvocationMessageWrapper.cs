// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR.Internal.Protocol;

namespace Microsoft.Azure.SignalR
{
    public enum HubInvocationType
    {
        OnConnected = 1,
        OnDisconnected = 2,
        OnOthers = 3
    }
    
    public class HubInvocationMessageWrapper : HubMessage
    {
        // TODO.
        // Optimization: replace string key with int key.
        // string key is only meaningful for read, but bad for performance
        // see https://jacksondunstan.com/articles/2527
        public const string ActionKeyName        = "action";
        public const string ConnectionIdKeyName  = "connId";
        public const string ConnectionIdsKeyName = "connIds";
        public const string ClaimsKeyName        = "claims";
        public const string ExcludedIdsKeyName   = "excluded";
        public const string GroupNameKeyName     = "group";
        public const string GroupNamesKeyName    = "groups";
        public const string UsersKeyName         = "users";
        public const string UserKeyName          = "user";
        public const string TimestampKeyName     = "_ts";
        public TransferFormat Type { get; }

        public HubInvocationType Target { get; set; }

        public HubMessage HubInvocationMessage { get; set; }

        public List<byte[]> Payload { get; private set; }

        public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>();

        public HubInvocationMessageWrapper(TransferFormat t)
        {
            Type = t;
            Target = HubInvocationType.OnOthers;
            // We only have two protocols: JSON and MessagePack
            Payload = new List<byte[]>(2) { null, null };
        }
    }
}
