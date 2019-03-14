// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Owin;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    internal class TestEndpointRouter : EndpointRouterDecorator
    {
        private readonly string _negotiateEndpoint;

        public TestEndpointRouter(string negotiateEndpoint) : base()
        {
            _negotiateEndpoint = negotiateEndpoint;
        }

        public override ServiceEndpoint GetNegotiateEndpoint(IOwinContext context, IEnumerable<ServiceEndpoint> primaryEndpoints)
        {
            return primaryEndpoints.First(e => e.ConnectionString == _negotiateEndpoint);
        }
    }
}