// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Testing.xunit;
using Microsoft.Azure.SignalR.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Tests;

public class SignalRServiceE2EFacts(ITestOutputHelper output) 
    : ServiceE2EFactsBase(new TestServerFactory(), new TestClientSetFactory(), output)
{
    public static object[][] TestData =
    [
        .. TestDataBase,
        ["TestClientIPEcho", DefaultClientCount, new Func<string, ITestClientSet, Task>((methodName, clients) => clients.SendAsync(methodName, sendCount : DefaultClientCount, messages : DefaultMessage))],
        ["TestClientUser", DefaultClientCount, new Func<string, ITestClientSet, Task>((methodName, clients) => clients.SendAsync(methodName, sendCount : DefaultClientCount, messages : DefaultMessage))],
        ["TestClientQueryString", DefaultClientCount, new Func<string, ITestClientSet, Task>((methodName, clients) => clients.SendAsync(methodName, sendCount : DefaultClientCount, messages : DefaultMessage))],
    ];

    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [MemberData(nameof(TestData))]
    public Task RunE2ETests(string methodName, int expectedMessageCount, Func<string, ITestClientSet, Task> coreTask)
    {
        return RunE2ETestsBase(methodName, expectedMessageCount, coreTask);
    }
}