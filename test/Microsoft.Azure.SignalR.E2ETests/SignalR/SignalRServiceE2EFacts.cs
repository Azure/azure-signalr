﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.SignalR.Tests.Common;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Tests
{
    public class SignalRServiceE2EFacts : ServiceE2EFactsBase
    {
        public SignalRServiceE2EFacts(ITestOutputHelper output) : base(new TestServerFactory(), new TestClientSetFactory(), output)
        {
        }
    }
}