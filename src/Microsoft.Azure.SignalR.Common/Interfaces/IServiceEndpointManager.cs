// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.SignalR;

internal delegate void EndpointEventHandler(HubServiceEndpoint endpoint);

internal interface IServiceEndpointManager
{
    IReadOnlyDictionary<ServiceEndpoint, ServiceEndpoint> Endpoints { get; }

    event EndpointEventHandler OnAdd;

    event EndpointEventHandler OnRemove;

    IServiceEndpointProvider GetEndpointProvider(ServiceEndpoint endpoint);

    IReadOnlyList<HubServiceEndpoint> GetEndpoints(string hub);
}
