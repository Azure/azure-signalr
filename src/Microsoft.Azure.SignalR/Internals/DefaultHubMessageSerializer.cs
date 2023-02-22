// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    // Copied from https://github.com/aspnet/AspNetCore/commit/3d93e095dbf2297fabf595099341c4cce673f32d
    // Planning on replacing in the 5.0 timeframe
    internal class DefaultHubMessageSerializer
    {
        private readonly Dictionary<string, IHubProtocol> _hubProtocols = new Dictionary<string, IHubProtocol>();

        public DefaultHubMessageSerializer(IHubProtocolResolver hubProtocolResolver, IList<string> globalSupportedProtocols, IList<string> hubSupportedProtocols)
        {
            var supportedProtocols = hubSupportedProtocols ?? globalSupportedProtocols ?? Array.Empty<string>();
            foreach (var protocolName in supportedProtocols)
            {
                // blazorpack is meant to only be used by the ComponentHub
                // We remove it from the list here except when the Hub has a single protocol that is "blazorpack" because
                // that identifies it as the ComponentHub
                if (supportedProtocols.Count > 1 && string.Equals(protocolName, Constants.Protocol.BlazorPack, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var protocol = hubProtocolResolver.GetProtocol(protocolName, (supportedProtocols as IReadOnlyList<string>) ?? supportedProtocols.ToList());
                if (protocol != null)
                {
                    // the latter one override the previous one
                    _hubProtocols[protocolName] = protocol;
                }
            }
        }

        public IReadOnlyList<SerializedMessage> SerializeMessage(HubMessage message)
        {
            var list = new List<SerializedMessage>(_hubProtocols.Count);
            foreach (var protocol in _hubProtocols)
            {
                list.Add(new SerializedMessage(protocol.Value.Name, protocol.Value.GetMessageBytes(message)));
            }
            return list;
        }

        #region Azure SignalR Service
        public ReadOnlyMemory<byte> SerializeMessage(string protocol, HubMessage message) =>
            _hubProtocols.FirstOrDefault(p => p.Key.Equals(protocol, StringComparison.OrdinalIgnoreCase)).Value.GetMessageBytes(message);
        #endregion
    }
}