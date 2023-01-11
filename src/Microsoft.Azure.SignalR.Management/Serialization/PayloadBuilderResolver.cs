// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Common;

#nullable enable

namespace Microsoft.Azure.SignalR.Management
{
    /// <summary>
    /// Used to keep backward compatibility after we support upload binary payload in the REST API. If users don't add any their own <see cref="IHubProtocol"/>, then they are legacy user and we still use JSON payload for them. If users have added hub protocols, then we can think the current hub protocols are all the protocols they want to support, and we don't need service-side JSON->MessagePack conversion even if there is only a JSON protocol.
    /// </summary>
    internal class PayloadBuilderResolver
    {
        private readonly IHubProtocolResolver _hubProtocolResolver;

        public PayloadBuilderResolver(IHubProtocolResolver hubProtocolResolver)
        {
            _hubProtocolResolver = hubProtocolResolver;
        }

        public IPayloadContentBuilder GetPayloadContentBuilder()
        {
            if (_hubProtocolResolver.AllProtocols.Count == 1 && _hubProtocolResolver.AllProtocols[0] is JsonObjectSerializerHubProtocol jsonObjectSerializerHubProtocol)
            {
                // The hub protocol is the default one. Use JSON payload so that the service will convert it to MessagePack and keep backward compatibility as users may depend on this feature for MessagePack client support.
                return new JsonPayloadContentBuilder(jsonObjectSerializerHubProtocol.ObjectSerializer);
            }
            else
            {
                // The hub protocols are not default. Users must have added or changed the protocols. No need to support service-side Json->MessagePack conversion.
                return new BinaryPayloadContentBuilder(_hubProtocolResolver.AllProtocols);
            }
        }
    }
}
