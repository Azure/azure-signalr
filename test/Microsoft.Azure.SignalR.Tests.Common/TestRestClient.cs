// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using Microsoft.Azure.SignalR.Tests.Common;

namespace Microsoft.Azure.SignalR.Tests
{
    public class TestRestClient : SignalRServiceRestClient
    {
        public TestRestClient(HttpStatusCode code, string content) : base(new TestRootHandler(code, content)) { }

        public TestRestClient(HttpStatusCode code) : base(new TestRootHandler(code)) { }

    }
}
