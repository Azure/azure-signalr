// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Management;

public abstract class StreamingManager : IStreamingManager
{
    public abstract Task SendStreamAsync<TItem>(string connectionId, string streamId, IAsyncEnumerable<TItem> items, CancellationToken cancellationToken);

    public abstract Task SendStreamAsync<TItem>(string connectionId, string streamId, ChannelReader<TItem> items, CancellationToken cancellationToken);
}
