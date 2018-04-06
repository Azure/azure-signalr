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
    
    public class ServiceMessage : HubMessage
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

        public TransferFormat Format { get; }

        public HubInvocationType InvocationType { get; set; }

        public byte[] JsonPayload { get; set; }

        public byte[] MsgpackPayload { get; set; }

        public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>();

        public ServiceMessage()
            : this (TransferFormat.Binary)
        {   
        }

        public ServiceMessage(TransferFormat format)
        {
            Format = format;
            InvocationType = HubInvocationType.OnOthers;
        }

        // Write the payload according to the specified format
        public void WritePayload(TransferFormat format, byte[] payload)
        {
            if (format == TransferFormat.Text)
            {
                JsonPayload = payload;
            }
            else
            {
                MsgpackPayload = payload;
            }
        }

        // Read the non-null payload if only payload is non-null
        // For example, the message from Service only contains one kind of payload.
        public byte[] ReadPayload()
        {
            return JsonPayload != null ? JsonPayload : MsgpackPayload;
        }
    }
}
