// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal class ClientConnectionFactory : IClientConnectionFactory
    {
        public ServiceConnectionContext CreateConnection(OpenConnectionMessage message)
        {
            return new ServiceConnectionContext(message);
        }
    }
}
