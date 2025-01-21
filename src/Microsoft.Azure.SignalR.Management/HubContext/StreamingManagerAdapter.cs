// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Management.HubContext;

namespace Microsoft.Azure.SignalR.Management
{
    internal class StreamingManagerAdapter : StreamingManager
    {
        private readonly ConcurrentDictionary<(string connectionId, string streamId), CancellationTokenSource> _cancellations = new();
        private readonly IStreamingHubLifetimeManager _lifetimeManager;

        public StreamingManagerAdapter(IStreamingHubLifetimeManager lifetimeManager)
        {
            _lifetimeManager = lifetimeManager;
        }

        public override void CancelStream(string connectionId, string streamId)
        {
            if (_cancellations.TryRemove((connectionId, streamId), out var cts))
            {
                cts.Cancel();
            }
        }

        public override async Task SendStream<TItem>(string connectionId, string streamId, IAsyncEnumerable<TItem> items)
        {
            var source = new CancellationTokenSource();
            if (!_cancellations.TryAdd((connectionId, streamId), source))
            {
                throw new InvalidOperationException("Cannot send a stream twice.");
            }
            try
            {
                await foreach (var item in items.WithCancellation(source.Token))
                {
                    await _lifetimeManager.SendStreamItemAsync(connectionId, streamId, item);
                }
                await _lifetimeManager.SendStreamCompletionAsync(connectionId, streamId, null);
            }
            catch (OperationCanceledException) when (source.Token.IsCancellationRequested)
            {
                // do not send anything if the stream is cancelled.
            }
            catch (Exception ex)
            {
                await _lifetimeManager.SendStreamCompletionAsync(connectionId, streamId, ex.Message);
            }
        }

        public override async Task SendStream<TItem>(string connectionId, string streamId, ChannelReader<TItem> channelReader)
        {
            var source = new CancellationTokenSource();
            if (!_cancellations.TryAdd((connectionId, streamId), source))
            {
                throw new InvalidOperationException("Cannot send a stream twice.");
            }
            try
            {
                while (await channelReader.WaitToReadAsync(source.Token))
                {
                    while (channelReader.TryRead(out var item))
                    {
                        await _lifetimeManager.SendStreamItemAsync(connectionId, streamId, item);
                    }
                }
                await _lifetimeManager.SendStreamCompletionAsync(connectionId, streamId, null);
            }
            catch (OperationCanceledException) when (source.Token.IsCancellationRequested)
            {
                // do not send anything if the stream is cancelled.
            }
            catch (Exception ex)
            {
                await _lifetimeManager.SendStreamCompletionAsync(connectionId, streamId, ex.Message);
            }
        }
    }
}