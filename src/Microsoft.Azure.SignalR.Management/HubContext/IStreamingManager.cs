// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Management;

/// <summary>
/// A streaming manager abstraction for sending stream response.
/// </summary>
internal interface IStreamingManager
{
    /// <summary>
    /// Send stream items to a connection.
    /// </summary>
    /// <typeparam name="TItem">The type of stream item.</typeparam>
    /// <param name="connectionId">The connection id.</param>
    /// <param name="streamId">The stream id.</param>
    /// <param name="items">The items in stream.</param>
    /// <param name="cancellationToken">The cancellation token to stop the stream.</param>
    Task SendStreamAsync<TItem>(string connectionId, string streamId, IAsyncEnumerable<TItem> items, CancellationToken cancellationToken);
    /// <summary>
    /// Send stream items to a connection.
    /// </summary>
    /// <typeparam name="TItem">The type of stream item.</typeparam>
    /// <param name="connectionId">The connection id.</param>
    /// <param name="streamId">The stream id.</param>
    /// <param name="items">The items in stream.</param>
    /// <param name="cancellationToken">The cancellation token to stop the stream.</param>
    Task SendStreamAsync<TItem>(string connectionId, string streamId, ChannelReader<TItem> items, CancellationToken cancellationToken);
}
