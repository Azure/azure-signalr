// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Protocols;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.AspNetCore.SignalR.Internal.Protocol;
using Microsoft.AspNetCore.Sockets.Client;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    public class CloudHubConnectionContext : HubConnectionContext
    {
        private static Action<object> _abortedCallback = AbortConnection;

        private readonly IConnection _connection;
        private readonly ILogger _logger;
        private readonly CancellationTokenSource _connectionAbortedTokenSource = new CancellationTokenSource();
        private readonly TaskCompletionSource<object> _abortCompletedTcs = new TaskCompletionSource<object>();

        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1);


        public CloudHubConnectionContext(IConnection connection, ConnectionContext connectionContext, TimeSpan keepAliveInterval, ILoggerFactory loggerFactory)
            : base(connectionContext, keepAliveInterval, loggerFactory)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _logger = loggerFactory.CreateLogger<CloudHubConnectionContext>();
        }

        internal virtual HubProtocolReaderWriter ProtocolReaderWriter { get; set; }

        internal ExceptionDispatchInfo AbortException { get; private set; }

        // Currently used only for streaming methods
        internal ConcurrentDictionary<string, CancellationTokenSource> ActiveRequestCancellationSources { get; } = new ConcurrentDictionary<string, CancellationTokenSource>();

        public override async Task WriteAsync(HubMessage message)
        {
            try
            {
                await _writeLock.WaitAsync();

                var buffer = ProtocolReaderWriter.WriteMessage(message);

                await _connection.SendAsync(buffer, CancellationToken.None);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public override void Abort()
        {
            // If we already triggered the token then noop, this isn't thread safe but it's good enough
            // to avoid spawning a new task in the most common cases
            if (_connectionAbortedTokenSource.IsCancellationRequested)
            {
                return;
            }

            // We fire and forget since this can trigger user code to run
            Task.Factory.StartNew(_abortedCallback, this);
        }

        internal void Abort(Exception exception)
        {
            AbortException = ExceptionDispatchInfo.Capture(exception);
            Abort();
        }

        private static void AbortConnection(object state)
        {
            var connection = (CloudHubConnectionContext)state;
            try
            {
                connection._connectionAbortedTokenSource.Cancel();

                // Communicate the fact that we're finished triggering abort callbacks
                connection._abortCompletedTcs.TrySetResult(null);
            }
            catch (Exception ex)
            {
                // TODO: Should we log if the cancellation callback fails? This is more preventative to make sure
                // we don't end up with an unobserved task
                connection._abortCompletedTcs.TrySetException(ex);
            }
        }

    }
}
