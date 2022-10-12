// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET7_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;

namespace Microsoft.Azure.SignalR
{
    // Modified from https://github.com/dotnet/aspnetcore/blob/v7.0.0-preview.7.22376.6/src/SignalR/common/Shared/ClientResultsManager.cs#L110
    // Custom TCS type links `cancellationToken.Cancel` to `tcs.SetCanceld()`. Besides, it provides a customized behaviour in `tcs.SetCanceld()`.
    // This modified version decoupled this class with `ClientResultsManager`, `connectionId` and `invocationId` which are used in `SetCanceled()`
    // This version uses a general `Action setCanceldAction` instead.

    // Custom TCS type to avoid the extra allocation that would be introduced if we managed the cancellation separately
    // Also makes it easier to keep track of the CancellationTokenRegistration for disposal
    internal sealed class TaskCompletionSourceWithCancellation<T> : TaskCompletionSource<T>
    {
        private readonly Action _trySetCanceledAction;
        private readonly CancellationToken _token;
        private CancellationTokenRegistration _tokenRegistration;

        public TaskCompletionSourceWithCancellation(CancellationToken cancellationToken, Action trySetCanceldAction)
            : base(TaskCreationOptions.RunContinuationsAsynchronously)
        {
            // Skip null check for cancellationToken because it never equals to null. 
            if (trySetCanceldAction == null)   
            {
                throw new ArgumentNullException(nameof(trySetCanceldAction));
            }
            _token = cancellationToken;
            _trySetCanceledAction = trySetCanceldAction;
        }

        public void RegisterCancellation()
        {
            if (_token.CanBeCanceled)
            {
                _tokenRegistration = _token.Register(static o =>
                {
                    var tcs = (TaskCompletionSourceWithCancellation<T>)o!;
                    tcs.TrySetCanceled();
                }, this);
            }
        }

        public new bool TrySetCanceled()
        {
            _trySetCanceledAction();
            return base.TrySetCanceled();
        }

        public new bool TrySetResult(T result)
        {
            _tokenRegistration.Dispose();
            return base.TrySetResult(result);
        }

        public new bool TrySetException(Exception exception)
        {
            _tokenRegistration.Dispose();
            return base.TrySetException(exception);
        }

#pragma warning disable IDE0060 // Remove unused parameter
        // Just making sure we don't accidentally call one of these without knowing
#if NETCOREAPP3_0_OR_GREATER
        public static new void SetCanceled(CancellationToken cancellationToken) => Debug.Assert(false);
#else
        public static void SetCanceled(CancellationToken cancellationToken) => Debug.Assert(false);
#endif

        public static new void SetException(IEnumerable<Exception> exceptions) => Debug.Assert(false);
        public static new void SetCanceled()
        {
            Debug.Assert(false);
        }
        public static new void TrySetCanceled(CancellationToken cancellationToken)
        {
            Debug.Assert(false);
        }
        public static new bool TrySetException(IEnumerable<Exception> exceptions)
        {
            Debug.Assert(false);
            return false;
        }
        public static new void SetException(Exception exception)
        {
            Debug.Assert(false);
        }
        public static new void SetResult(T result)
        {
            Debug.Assert(false);
        }
#pragma warning restore IDE0060 // Remove unused parameter
    }
}
#endif