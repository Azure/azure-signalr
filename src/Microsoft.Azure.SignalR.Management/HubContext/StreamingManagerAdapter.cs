// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Microsoft.Azure.SignalR.Management.HubContext;

namespace Microsoft.Azure.SignalR.Management;

internal class StreamingManagerAdapter : StreamingManager
{
    private readonly IStreamingHubLifetimeManager _lifetimeManager;

    public StreamingManagerAdapter(IStreamingHubLifetimeManager lifetimeManager)
    {
        _lifetimeManager = lifetimeManager;
    }

    public override async Task SendStreamAsync<TItem>(string connectionId, string streamId, IAsyncEnumerable<TItem> items, CancellationToken cancellationToken = default)
    {
        bool isCompleted = false;
        try
        {
            await foreach (var item in items.WithCancellation(cancellationToken))
            {
                await _lifetimeManager.SendStreamItemAsync(connectionId, streamId, item, cancellationToken);
            }
            isCompleted = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // do not send anything if the stream is cancelled.
        }
        catch (Exception ex)
        {
            await _lifetimeManager.SendStreamCompletionAsync(connectionId, streamId, ex.Message, cancellationToken);
        }
        if (isCompleted)
        {
            await _lifetimeManager.SendStreamCompletionAsync(connectionId, streamId, null, cancellationToken);
        }
    }

    public override async Task SendStreamAsync<TItem>(string connectionId, string streamId, ChannelReader<TItem> channelReader, CancellationToken cancellationToken = default)
    {
        bool isCompleted = false;
        try
        {
            while (await channelReader.WaitToReadAsync(cancellationToken))
            {
                while (channelReader.TryRead(out var item))
                {
                    await _lifetimeManager.SendStreamItemAsync(connectionId, streamId, item, cancellationToken);
                }
            }
            isCompleted = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // do not send anything if the stream is cancelled.
        }
        catch (Exception ex)
        {
            await _lifetimeManager.SendStreamCompletionAsync(connectionId, streamId, ex.Message, cancellationToken);
        }
        if (isCompleted)
        {
            await _lifetimeManager.SendStreamCompletionAsync(connectionId, streamId, null, cancellationToken);
        }
    }
}
