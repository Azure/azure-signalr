// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    internal class DefaultHubMessageSerializer
    {
        private readonly List<IHubProtocol> _hubProtocols = new List<IHubProtocol>();

        public DefaultHubMessageSerializer(IHubProtocolResolver hubProtocolResolver, IList<string> globalSupportedProtocols, IList<string> hubSupportedProtocols)
        {
            var supportedProtocols = hubSupportedProtocols ?? globalSupportedProtocols ?? Array.Empty<string>();
            foreach (var protocolName in supportedProtocols)
            {
                var protocol = hubProtocolResolver.GetProtocol(protocolName, (supportedProtocols as IReadOnlyList<string>) ?? supportedProtocols.ToList());
                if (protocol != null)
                {
                    _hubProtocols.Add(protocol);
                }
            }
        }

        public IReadOnlyList<SerializedMessage> SerializeMessage(HubMessage message)
        {
            var list = new List<SerializedMessage>(_hubProtocols.Count);
            foreach (var protocol in _hubProtocols)
            {
                if (_hubProtocols.Count > 1 && string.Equals(protocol.Name, "blazorpack", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                list.Add(new SerializedMessage(protocol.Name, protocol.GetMessageBytes(message)));
            }

            return list;
        }
    }
}