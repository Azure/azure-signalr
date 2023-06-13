// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Net.Http;
using Microsoft.AspNetCore.SignalR.Protocol;

#nullable enable

namespace Microsoft.Azure.SignalR.Common
{
    internal class BinaryPayloadContentBuilder : IPayloadContentBuilder
    {
        private readonly IReadOnlyList<IHubProtocol> _hubProtocols;
        public BinaryPayloadContentBuilder(IReadOnlyList<IHubProtocol> hubProtocols)
        {
            _hubProtocols = hubProtocols;
        }

        public HttpContent? Build(PayloadMessage? payload)
        {
            return payload == null ? null : (HttpContent)new BinaryPayloadMessageContent(payload, _hubProtocols);
        }
    }
}
