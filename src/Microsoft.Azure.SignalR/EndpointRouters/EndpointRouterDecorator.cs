// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    public class EndpointRouterDecorator : IEndpointRouter
    {
        private readonly IEndpointRouter _inner;

        public EndpointRouterDecorator(IEndpointRouter router = null)
        {
            _inner = router ?? new DefaultEndpointRouter(null);
        }

        public EndpointRouterDecorator(IEndpointRouter router = null, IOptions<ServiceOptions> options = null)
        {
            _inner = router ?? new DefaultEndpointRouter(options);
        }

        public virtual ServiceEndpoint GetNegotiateEndpoint(HttpContext context, IEnumerable<ServiceEndpoint> endpoints)
        {
            return _inner.GetNegotiateEndpoint(context, endpoints);
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
}
