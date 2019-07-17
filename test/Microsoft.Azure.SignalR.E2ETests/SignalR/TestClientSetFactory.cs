// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.SignalR.Tests.Common;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Tests
{
    internal class TestClientSetFactory : ITestClientSetFactory
    {
        public ITestClientSet Create(string serverUrl, int count, ITestOutputHelper output)
        {
            return new TestClientSet(serverUrl, count, output);
        }
    }
}
