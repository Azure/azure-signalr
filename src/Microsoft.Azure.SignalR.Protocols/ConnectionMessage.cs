// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Security.Claims;

namespace Microsoft.Azure.SignalR.Protocol
{
    public abstract class ConnectionMessage : ServiceMessage
    {
        protected ConnectionMessage(string connectionId)
        {
            ConnectionId = connectionId;
        }

        public string ConnectionId { get; set; }
    }

    public class OpenConnectionMessage : ConnectionMessage
    {
        public OpenConnectionMessage(string connectionId, Claim[] claims) : base(connectionId)
        {
            Claims = claims;
        }

        public Claim[] Claims { get; set; }
    }

    public class CloseConnectionMessage : ConnectionMessage
    {
        public CloseConnectionMessage(string connectionId, string errorMessage) : base(connectionId)
        {
            ErrorMessage = errorMessage;
        }

        public string ErrorMessage { get; set; }
    }

    public class ConnectionDataMessage : ConnectionMessage
    {
        public ConnectionDataMessage(string connectionId, ReadOnlyMemory<byte> payload) : base(connectionId)
        {
            Payload = payload;
        }

        public ReadOnlyMemory<byte> Payload { get; set; }
    }
}
