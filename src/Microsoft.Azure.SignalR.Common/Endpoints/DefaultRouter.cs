// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.SignalR
{
    internal class DefaultRouter : IEndpointRouter
    {
        public ServiceEndpoint GetNegotiateEndpoint(IReadOnlyList<ServiceEndpoint> primaryEndpoints)
        {
            // get primary endpoints snapshot
            return primaryEndpoints[StaticRandom.Next(primaryEndpoints.Count)];
        }
    }
}
