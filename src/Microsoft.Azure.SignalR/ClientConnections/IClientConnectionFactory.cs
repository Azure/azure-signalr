// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Http;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal interface IClientConnectionFactory
    {
        ServiceConnectionContext CreateConnection(OpenConnectionMessage message);

#if NETCOREAPP3_0
        ServiceConnectionContext CreateConnection(OpenConnectionMessage message, Endpoint endpoint);
#endif
    }
}
