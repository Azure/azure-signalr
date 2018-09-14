// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Tests
{
    internal class TestServiceHubDispatcher<THub> : ServiceHubDispatcher<THub> where THub : Hub
    {
        public TestServiceHubDispatcher()
            : base(null, null, null, null, Options.Create(new ServiceOptions()), NullLoggerFactory.Instance, null)
        {
        }

        public override void Start()
        {
        }
    }
}
