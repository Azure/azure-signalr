// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.SignalR.Protocol
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

        public static bool TryParseMessage(ref ReadOnlySequence<byte> input, out RMessage rm)
        {
            // use slice to get end position
            var bytes = input.ToArray();
            var s = Encoding.UTF8.GetString(bytes);
            // Find the first valid parentheses
            int lp = 1;
            int rp = 0;
            int index = 1;
            while (index < s.Length && lp > rp)
            {
                if (s[index] == '{') lp++;
                if (s[index] == '}') rp++;
                index++;
            }
            if (lp > rp)
            {
                rm = null;
                return false;
            }

            var sl = input.Slice(0, index);

            rm = ParseMessage(sl.ToArray());
            input = input.Slice(index);

            return true;
        }

        public static RMessage ParseMessage(byte[] payload)
        {
            var s = Encoding.UTF8.GetString(payload);
            RMessage rm = JsonConvert.DeserializeObject<RMessage>(s, new JsonSerializerSettings
            {
                Error = delegate (object sender, Newtonsoft.Json.Serialization.ErrorEventArgs errorArgs)
                {
                    var currentError = errorArgs.ErrorContext.Error.Message;
                    errorArgs.ErrorContext.Handled = true;
                }
            });
            return rm;
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
