// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Common;

#nullable enable

namespace Microsoft.Azure.SignalR.Management
{
    internal class PayloadBuilderResolver
    {
        private readonly IHubProtocolResolver _hubProtocolResolver;

        public PayloadBuilderResolver(IHubProtocolResolver hubProtocolResolver)
        {
            _hubProtocolResolver = hubProtocolResolver;
        }

        public IPayloadContentBuilder GetPayloadContentBuilder()
        {
            if (_hubProtocolResolver.AllProtocols.Count == 1 && _hubProtocolResolver.AllProtocols.Single() is JsonObjectSerializerHubProtocol jsonObjectSerializerHubProtocol)
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
