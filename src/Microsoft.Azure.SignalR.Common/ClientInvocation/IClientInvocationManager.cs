// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR
{
    internal interface IClientInvocationManager
    {
        ICallerClientResultsManager Caller { get; }
        IRoutedClientResultsManager Router { get; }

        bool TryGetInvocationReturnType(string invocationId, out Type type);

        void CleanupInvocationsByConnection(string connectionId);
    }
}