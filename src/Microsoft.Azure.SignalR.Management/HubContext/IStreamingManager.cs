// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Management
{
    /// <summary>
    /// A streaming manager abstraction for sending stream response.
    /// </summary>
    internal interface IStreamingManager
    {
        Task SendStream<TItem>(string connectionId, string streamId, IAsyncEnumerable<TItem> items);
        Task SendStream<TItem>(string connectionId, string streamId, ChannelReader<TItem> items);
        void CancelStream(string connectionId, string streamId);
    }
}