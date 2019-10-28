// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    // Copied from https://github.com/aspnet/AspNetCore/commit/3d93e095dbf2297fabf595099341c4cce673f32d
    // Planning on replacing in the 5.0 timeframe
    internal class DefaultHubMessageSerializer
    {
        private readonly List<IHubProtocol> _hubProtocols = new List<IHubProtocol>();

        public DefaultHubMessageSerializer(IHubProtocolResolver hubProtocolResolver, IList<string> globalSupportedProtocols, IList<string> hubSupportedProtocols)
        {
            var supportedProtocols = hubSupportedProtocols ?? globalSupportedProtocols ?? Array.Empty<string>();
            foreach (var protocolName in supportedProtocols)
            {
                // blazorpack is meant to only be used by the ComponentHub
                // We remove it from the list here except when the Hub has a single protocol that is "blazorpack" because
                // that identifies it as the ComponentHub
                if (supportedProtocols.Count > 1 && string.Equals(protocolName, "blazorpack", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

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
                list.Add(new SerializedMessage(protocol.Name, protocol.GetMessageBytes(message)));
            }

            return list;
        }
    }
}