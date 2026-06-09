// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;

using Microsoft.AspNetCore.Connections;

using Newtonsoft.Json;

namespace Microsoft.Azure.SignalR.Serverless.Common;

internal class ServerlessProtocol
{
    public class Constants
    {
        public static class Categories
        {
            public const string MessageCategory = "messages";
            public const string ConnectionCategory = "connections";
        }

        public static class Events
        {
            public const string ConnectEvent = "connect";
            public const string DisconnectEvent = "disconnect";
            public const string MessageEvent = "message";
            public const string ConnectedEvent = "connected";
            public const string DisconnectedEvent = "disconnected";
        }

        /// <summary>
        /// Represents the invocation message type.
        /// </summary>
        public const int InvocationMessageType = 1;

        /// <summary>
        /// Represents the ping message type.
        /// </summary>
        public const int PingMessageType = 6;

        // Reserve number in HubProtocolConstants

        /// <summary>
        /// Represents the open connection message type.
        /// </summary>
        public const int OpenConnectionMessageType = 10;

        /// <summary>
        /// Represents the close connection message type.
        /// </summary>
        public const int CloseConnectionMessageType = 11;
    }

    public class OpenConnectionMessage
    {
        [JsonProperty(PropertyName = "type")]
        public int Type { get; set; }
    }

    public class CloseConnectionMessage
    {
        [JsonProperty(PropertyName = "type")]
        public int Type { get; set; }

        [JsonProperty(PropertyName = "error")]
        public string Error { get; set; }
    }

    public class InvocationMessage
    {
        public ReadOnlySequence<byte> Payload { get; private set; }

        public string Target { get; }

        public string InvocationId { get; }

        public TransferFormat? Format { get; }

        public InvocationMessage(ReadOnlySequence<byte> payload, string target = null, string invocationId = null, TransferFormat? format = null)
        {
            Payload = payload;
            Target = target;
            InvocationId = invocationId;
            Format = format;
        }
    }
}
