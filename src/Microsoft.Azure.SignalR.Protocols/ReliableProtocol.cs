// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text;
using Newtonsoft.Json;

namespace Microsoft.Azure.SignalR.Protocol
{
    public class ReliableProtocol
    {
        public enum RMType
        {
            Data,
            Reload,
            Barrier,
            ACK,
            Dummy
        }
        public static RMessage TryParseMessage(byte[] payload)
        {
            string s = Encoding.UTF8.GetString(payload);
            return JsonConvert.DeserializeObject<RMessage>(s);
        }

        public static ReadOnlyMemory<byte> EncodeMessage(RMessage RM)
        {
            string s = JsonConvert.SerializeObject(RM);
            return Encoding.UTF8.GetBytes(s);
        }

        public class RMessage
        {
            public RMType MessageType { get; set; }
            // 0 : DataMessages
            // 1 : ReloadMessage
            // 2 : BarrierMessage
            // 3 : AckMessage
            // 4 : DummyMessage
            public string Payload { get; set; }
        }

        public class ReloadMessage
        {
            public string url { get; set; }
            public string token { get; set; }
        }

        public class BarrierMessage
        {
            public string from { get; set; }
            public string to { get; set; }
        }

        public class ReloadAckMessage
        {
            // This connection is over.
            public string oldConnID { get; set; }
        }
    }
}
