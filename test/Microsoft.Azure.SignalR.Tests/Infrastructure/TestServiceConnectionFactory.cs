// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR.Tests
{
    internal sealed class TestServiceConnectionFactory : IServiceConnectionFactory
    {
        private readonly Func<ServerConnectionType, IServiceConnection> _factoryFunc;

        public TestServiceConnectionFactory(Func<ServerConnectionType, IServiceConnection> factoryFunc)
        {
            _factoryFunc = factoryFunc;
        }

        public IServiceConnection Create(IConnectionFactory connectionFactory, IServiceMessageHandler serviceMessageHandler,
            ServerConnectionType type)
        {
            return _factoryFunc(type);
        }
    }
}
