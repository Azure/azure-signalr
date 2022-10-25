// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal interface IClientResultsManager
    {
        bool TryCompleteResult(string connectionId, CompletionMessage message);

        bool TryGetInvocationReturnType(string invocationId, out Type type);

        void CleanupInvocationsByConnection(string connectionId);
    }
}
