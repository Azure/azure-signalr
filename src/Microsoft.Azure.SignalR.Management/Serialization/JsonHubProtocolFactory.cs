// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management
{
    internal class JsonHubProtocolFactory
    {
        private readonly ServiceManagerOptions _serviceManagerOptions;

        public JsonHubProtocolFactory(IOptions<ServiceManagerOptions> options)
        {
            _serviceManagerOptions = options.Value;
        }

        public IHubProtocol GetJsonHubProtocol()
        {
            return _serviceManagerOptions.ObjectSerializer != null
                ? new JsonObjectSerializerHubProtocol(_serviceManagerOptions.ObjectSerializer)
                : new JsonHubProtocol();
        }
    }
}
