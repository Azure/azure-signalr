// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.IntegrationTests.Infrastructure
{
    // The only reason for having this interface is to overcome the lack
    // of static properties support in generic type system
    internal interface IIntegrationTestStartupParameters
    {
        public int ConnectionCount { get; }
        public ServiceEndpoint[] ServiceEndpoints { get; }
        public GracefulShutdownMode ShutdownMode { get; }
    }
}
