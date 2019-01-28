// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    internal sealed class TestServiceConnectionManager : SignalR.IServiceConnectionManager
    {
        public TestServiceConnectionManager()
        {
        }

        public IServiceConnection CreateServiceConnection()
        {
            throw new NotImplementedException();
        }

        public void DisposeServiceConnection(IServiceConnection connection)
        {
        }
    }
}
