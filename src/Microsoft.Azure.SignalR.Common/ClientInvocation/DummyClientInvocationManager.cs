// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    internal sealed class DummyClientInvocationManager : IClientInvocationManager
    {
        public ICallerClientResultsManager Caller => throw new NotSupportedException();
        public IRoutedClientResultsManager Router => throw new NotSupportedException();

        public DummyClientInvocationManager()
        {
        }
    }
}
