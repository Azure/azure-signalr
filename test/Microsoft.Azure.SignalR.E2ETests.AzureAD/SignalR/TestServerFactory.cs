// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.SignalR.Tests.Common;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Tests
{
    internal class TestServerFactory : ITestServerFactory
    {
        public ITestServer Create(ITestOutputHelper output)
        {
            return new TestServer(output);
        }
    }
}
