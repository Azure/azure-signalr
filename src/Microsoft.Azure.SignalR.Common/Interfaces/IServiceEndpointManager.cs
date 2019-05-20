// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.SignalR
{
    internal interface IServiceEndpointManager
    {
        IServiceEndpointProvider GetEndpointProvider(ServiceEndpoint endpoint);

        ServiceEndpoint[] Endpoints { get; }
    }
}
