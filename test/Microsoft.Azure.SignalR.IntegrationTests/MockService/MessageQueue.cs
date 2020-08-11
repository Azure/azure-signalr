// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.IntegrationTests.MockService
{
    /// <summary>
    /// Not a general purpose producer-consumer async queue implementation
    /// Thread safe for only single concurrent reader + single concurrent writer
    /// </summary>
    class MessageQueue<T> where T : class
    {
        private object _lock = new object();
        private ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();
        private TaskCompletionSource<bool> _tcs = null;

        public void EnqueueMessage(T m)
        {
            lock (_lock)
            {
                _queue.Enqueue(m);
                if (_tcs != null)
                {
                    _tcs.TrySetResult(true);
                    _tcs = null;
                }
            }
        }

        public async Task<T> DequeueMessageAsync()
        {
            TaskCompletionSource<bool> tcs = null;
            lock (_lock)
            {
                if (_queue.TryDequeue(out var msg))
                {
                    return msg;
                }
                tcs = _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            await tcs.Task;
            _queue.TryDequeue(out var m);
            Debug.Assert(m != null);
            return m;
        }

        public async Task<T> PeekMessageAsync()
        {
            TaskCompletionSource<bool> tcs = null;
            lock (_lock)
            {
                if (_queue.TryPeek(out var msg))
                {
                    return msg;
                }
                tcs = _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            await tcs.Task;
            _queue.TryPeek(out var m);
            Debug.Assert(m != null);
            return m;
        }
    }
}
