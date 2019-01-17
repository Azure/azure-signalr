// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.SignalR
{
    internal interface IServiceEndpointManager
    {
        IServiceEndpointProvider GetEndpointProvider(ServiceEndpoint endpoint);

        IReadOnlyList<ServiceEndpoint> GetAvailableEndpoints();

        IReadOnlyList<ServiceEndpoint> GetPrimaryEndpoints();

        ServiceEndpoint[] Endpoints { get; }
    }
}
