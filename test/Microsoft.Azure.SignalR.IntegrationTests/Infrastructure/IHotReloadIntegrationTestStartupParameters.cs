// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System.Collections.Generic;

namespace Microsoft.Azure.SignalR.IntegrationTests.Infrastructure
{
    // Specialized startup for hot reload config tests
    internal interface IHotReloadIntegrationTestStartupParameters : IIntegrationTestStartupParameters
    {
        public KeyValuePair<string, string>[] Endpoints(int versionIndex);
        public int EndpointsCount { get; }
    }
}
