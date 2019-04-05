// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Http;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal class ClientConnectionFactory : IClientConnectionFactory
    {
        public ServiceConnectionContext CreateConnection(OpenConnectionMessage message)
        {
            return new ServiceConnectionContext(message);
        }

#if NETCOREAPP3_0
        public ServiceConnectionContext CreateConnection(OpenConnectionMessage message, Endpoint endpoint)
        {
            return new ServiceConnectionContext(message, endpoint);
        }
#endif
    }
}
