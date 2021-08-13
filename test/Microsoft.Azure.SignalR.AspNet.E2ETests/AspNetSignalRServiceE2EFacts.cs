// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    public class AspNetSignalRServiceE2EFacts : ServiceE2EFactsBase
    {
        public AspNetSignalRServiceE2EFacts(ITestOutputHelper output)
            : base(new TestServerFactory(), new TestClientSetFactory(), output)
        {
        }

        [SkipIfConnectionStringNotPresentTheory]
        [MemberData(nameof(TestDataBase))]
        public Task RunE2ETests(string methodName, int expectedMessageCount, Func<string, ITestClientSet, Task> coreTask)
        {
            return RunE2ETestsBase(methodName, expectedMessageCount, coreTask);
        }
    }
}