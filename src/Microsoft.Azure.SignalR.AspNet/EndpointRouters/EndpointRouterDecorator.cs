// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Owin;

namespace Microsoft.Azure.SignalR.AspNet;

public class EndpointRouterDecorator : IEndpointRouter
{
    private readonly IEndpointRouter _inner;

    public EndpointRouterDecorator(IEndpointRouter router = null)
    {
        _inner = router ?? new DefaultEndpointRouter();
    }

    public virtual ServiceEndpoint GetNegotiateEndpoint(IOwinContext owinContext, IEnumerable<ServiceEndpoint> endpoints)
    {
        return _inner.GetNegotiateEndpoint(owinContext, endpoints);
    }

    public virtual IEnumerable<ServiceEndpoint> GetEndpointsForBroadcast(IEnumerable<ServiceEndpoint> endpoints)
    {
        return _inner.GetEndpointsForBroadcast(endpoints);
    }

    public virtual IEnumerable<ServiceEndpoint> GetEndpointsForConnection(string connectionId, IEnumerable<ServiceEndpoint> endpoints)
    {
        return _inner.GetEndpointsForConnection(connectionId, endpoints);
    }

    public virtual IEnumerable<ServiceEndpoint> GetEndpointsForGroup(string groupName, IEnumerable<ServiceEndpoint> endpoints)
    {
        return _inner.GetEndpointsForGroup(groupName, endpoints);
    }

    public virtual IEnumerable<ServiceEndpoint> GetEndpointsForUser(string userId, IEnumerable<ServiceEndpoint> endpoints)
    {
        return _inner.GetEndpointsForUser(userId, endpoints);
    }
}
