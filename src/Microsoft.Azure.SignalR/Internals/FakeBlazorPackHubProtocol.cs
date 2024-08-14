// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Protocol;

using SignalRProtocol = Microsoft.AspNetCore.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal sealed class FakeBlazorPackHubProtocol : SignalRProtocol.IHubProtocol
    {
        private static readonly SignalRProtocol.HubMessage Invocation = new SignalRProtocol.InvocationMessage("a", Array.Empty<object>());
        private static readonly SignalRProtocol.HubMessage Close = new SignalRProtocol.CloseMessage(string.Empty);
        private static readonly SignalRProtocol.HubMessage Data = SignalRProtocol.PingMessage.Instance;
        
        internal static SignalRProtocol.IHubProtocol Instance = new FakeBlazorPackHubProtocol();

        public string Name => "blazorpack";

        public TransferFormat TransferFormat => TransferFormat.Binary;

        public int Version => 1;

        public ReadOnlyMemory<byte> GetMessageBytes(SignalRProtocol.HubMessage message) =>
            throw new NotSupportedException();

        public bool IsVersionSupported(int version) => version <= Version;

        public bool TryParseMessage(ref ReadOnlySequence<byte> input, IInvocationBinder binder, out SignalRProtocol.HubMessage message)
        {
            if (!BinaryMessageParser.TryParseMessage(ref input, out var payload))
            {
                message = null;
                return false;
            }
            var first = payload.First.Span;
            if (first.Length < 2)
            {
                first = payload.Slice(0, 2).ToArray();
            }
            // we do not want to refer message pack or Microsoft.AspNetCore.SignalR.Protocols.MessagePack
            // So we parse message pack directly.
            // ref: https://github.com/msgpack/msgpack/blob/master/spec.md
            if (first[0] > 0x90 && first[0] < 0x9f && first[1] <= 128)
            {
                var type = first[1];
                switch (type)
                {
                    case 1: // Invocation
                    case 2: // StreamItem
                    case 3: // Completion
                    case 4: // StreamInvocation
                    case 5: // CancelInvocation
                        message = Invocation;
                        return true;
                    case 7: // Close
                        message = Close;
                        return true;
                    case 6: // Ping
                        message = Data;
                        return true;
                    default:
                        message = null;
                        return false;
                }
            }
            message = null;
            return false;
        }

        public void WriteMessage(SignalRProtocol.HubMessage message, IBufferWriter<byte> output) =>
            throw new NotSupportedException();
    }
}
