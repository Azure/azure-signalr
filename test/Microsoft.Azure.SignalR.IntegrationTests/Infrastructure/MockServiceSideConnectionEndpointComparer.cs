// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.SignalR.IntegrationTests.MockService;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Azure.SignalR.IntegrationTests.Infrastructure
{
    internal class MockServiceSideConnectionEndpointComparer : IEqualityComparer<MockServiceSideConnection>
    {
        public bool Equals(MockServiceSideConnection x, MockServiceSideConnection y)
        {
            return x.Endpoint.Endpoint == y.Endpoint.Endpoint && x.Endpoint.EndpointType == y.Endpoint.EndpointType;
        }

        public int GetHashCode([DisallowNull] MockServiceSideConnection obj) => obj.Endpoint.GetHashCode();
    }
}
