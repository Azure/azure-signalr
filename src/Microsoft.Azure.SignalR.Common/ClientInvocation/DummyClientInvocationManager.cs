// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR
{
    internal sealed class DummyClientInvocationManager : IClientInvocationManager
    {
        public ICallerClientResultsManager Caller => throw new NotSupportedException();
        public IRoutedClientResultsManager Router => throw new NotSupportedException();

        public DummyClientInvocationManager()
        {
        }

        public void CleanupInvocationsByConnection(string connectionId) => throw new NotSupportedException();

        public bool TryGetInvocationReturnType(string invocationId, out Type type) => throw new NotSupportedException();
    }
}
