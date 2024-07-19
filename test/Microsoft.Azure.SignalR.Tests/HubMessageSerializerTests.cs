// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.AspNetCore.SignalR.Protocol;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests;

public class HubMessageSerializerTests
{
    [Fact]
    public void HubMessageSerializerFact()
    {
        var hubProtocol1 = new JsonHubProtocol();
        var hubProtocol2 = new IgnoreCaseJsonHubProtocol();
        var availableProtocols = new List<IHubProtocol>() { hubProtocol1, hubProtocol2 };
        var supportedProtocols = availableProtocols.Select(p => p.Name).ToList();
        var resolver = new DefaultHubProtocolResolver(availableProtocols, default);
        var serializer = new DefaultHubMessageSerializer(resolver, null, supportedProtocols);
        var message = new CancelInvocationMessage("123");
        serializer.SerializeMessage("JsOn", message);
    }

    private class IgnoreCaseJsonHubProtocol : IHubProtocol
    {
        public string Name => "json";

        public int Version => 0;

        public TransferFormat TransferFormat => default;

        public ReadOnlyMemory<byte> GetMessageBytes(HubMessage message)
        {
            return default;
        }

        public bool IsVersionSupported(int version)
        {
            return true;
        }

        public bool TryParseMessage(ref ReadOnlySequence<byte> input, IInvocationBinder binder, [NotNullWhen(true)] out HubMessage message)
        {
            message = default;
            return true;
        }

        public void WriteMessage(HubMessage message, IBufferWriter<byte> output)
        {
        }
    }
}
