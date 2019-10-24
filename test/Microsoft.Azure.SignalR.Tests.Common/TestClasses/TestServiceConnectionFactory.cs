// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR.Tests.Common
{
    internal sealed class TestServiceConnectionFactory : IServiceConnectionFactory
    {
        private readonly Func<ServiceEndpoint, IServiceConnection> _generator;
        public TestServiceConnectionFactory(Func<ServiceEndpoint, IServiceConnection> generator = null)
        {
            _generator = generator;
        }

        public IServiceConnection Create(HubServiceEndpoint endpoint, IServiceMessageHandler serviceMessageHandler, ServerConnectionType type)
        {
            return _generator?.Invoke(endpoint) ?? new TestServiceConnection();
        }
    }
}
