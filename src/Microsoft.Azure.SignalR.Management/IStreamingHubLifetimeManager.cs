// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Management;

internal interface IStreamingHubLifetimeManager
{
    Task SendStreamItemAsync<TItem>(string connectionId, string streamId, TItem item, CancellationToken cancellationToken = default);

    Task SendStreamCompletionAsync(string connectionId, string streamId, string error, CancellationToken cancellationToken = default);
}
