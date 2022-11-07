// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;

namespace Microsoft.Azure.SignalR
{
    internal interface IRoutedClientResultsManager : IClientResultsManager
    {
        void AddInvocation(string connectionId, string invocationId, string callerServerId, CancellationToken cancellationToken);
    }
}