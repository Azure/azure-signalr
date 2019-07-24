// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.SignalR.Tests.Common;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    public class AspNetSignalRServiceE2EFacts : ServiceE2EFactsBase
    {
        public AspNetSignalRServiceE2EFacts(ITestOutputHelper output)
            : base(new TestServerFactory(), new TestClientSetFactory(), output)
        {
        }
    }
}