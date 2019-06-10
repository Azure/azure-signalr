// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.SignalR.Tests.Common;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    internal class TestServerFactory : ITestServerFactory
    {
        public ITestServer Create()
        {
            return new TestServer();
        }
    }
}
