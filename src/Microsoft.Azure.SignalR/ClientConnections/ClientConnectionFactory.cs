// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Http;
using Microsoft.Azure.SignalR.Protocol;
using System;

namespace Microsoft.Azure.SignalR
{
    internal class ClientConnectionFactory : IClientConnectionFactory
    {
        public ServiceConnectionContext CreateConnection(OpenConnectionMessage message, Action<HttpContext> configureContext = null)
        {
            return new ServiceConnectionContext(message, configureContext);
        }
    }
}
