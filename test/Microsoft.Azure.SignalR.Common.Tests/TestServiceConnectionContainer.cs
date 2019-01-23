// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Azure.SignalR.Tests
{
    internal sealed class TestServiceConnectionContainer : ServiceConnectionContainerBase
    {
        public TestServiceConnectionContainer(List<IServiceConnection> serviceConnections, ServiceEndpoint endpoint = null) : base(null, null, serviceConnections, endpoint)
        {
        }

        public override IServiceConnection CreateServiceConnection()
        {
            throw new NotImplementedException();
        }

        public override void DisposeServiceConnection(IServiceConnection connection)
        {
            throw new NotImplementedException();
        }

        protected override IServiceConnection CreateServiceConnectionCore()
        {
            throw new NotImplementedException();
        }
    }
}
