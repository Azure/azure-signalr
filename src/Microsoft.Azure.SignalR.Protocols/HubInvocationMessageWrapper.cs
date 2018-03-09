// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR.Internal.Protocol;

namespace Microsoft.Azure.SignalR
{
    public class HubInvocationServiceConstants
    {
        public const int OnConnected = 0;
        public const int OnDisconnected = 1;
        public const int OnOthers = 2;
    }
    
    public class HubInvocationMessageWrapper : HubMessage
    {
        public const string ActionKeyName        = "action";
        public const string ConnectionIdKeyName  = "connId";
        public const string ConnectionIdsKeyName = "connIds";
        public const string ClaimsKeyName        = "claims";
        public const string ExcludedIdsKeyName   = "excluded";
        public const string GroupNameKeyName     = "group";
        public const string GroupNamesKeyName    = "groups";
        public const string UsersKeyName         = "users";
        public const string UserKeyName          = "user";

        public TransferFormat Type { get; }

        public int Target { get; set; }

        public HubMessage HubInvocationMessage { get; set; }

        public byte[] Payload { get; set; }

        // PayloadExt is for two different kinds of encoded data.
        // In this case, Payload saves JSON format, PayloadExt has messagepack format.
        // Otherwise, PayloadExt is null.
        public byte[] PayloadExt { get; set; }

        public IDictionary<string, string> Metadata { get; } = new Dictionary<string, string>();

        public HubInvocationMessageWrapper(TransferFormat t)
        {
            Type = t;
            Target = HubInvocationServiceConstants.OnOthers;
        }
    }
}
