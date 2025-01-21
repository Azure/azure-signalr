// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Management.HubContext
{
    internal abstract class StreamingManager : IStreamingManager
    {
        public abstract void CancelStream(string connectionId, string streamId);

        public abstract Task SendStream<TItem>(string connectionId, string streamId, IAsyncEnumerable<TItem> items);

        public abstract Task SendStream<TItem>(string connectionId, string streamId, ChannelReader<TItem> items);
    }
}
