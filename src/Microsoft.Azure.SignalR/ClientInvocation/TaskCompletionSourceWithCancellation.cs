// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        private readonly Action _setCanceledAction;
        private readonly CancellationToken _token;
        private CancellationTokenRegistration _tokenRegistration;

        public TaskCompletionSourceWithCancellation(CancellationToken cancellationToken, Action setCanceldAction)
            : base(TaskCreationOptions.RunContinuationsAsynchronously)
        {
            _token = cancellationToken;
            _setCanceledAction = setCanceldAction;
        }

        public void RegisterCancellation()
        {
            if (_token.CanBeCanceled)
            {
                _tokenRegistration = _token.Register(static o =>
                {
                    var tcs = (TaskCompletionSourceWithCancellation<T>)o!;
                    tcs.SetCanceled();
                }, this);
            }
        }

        public new void SetCanceled() => _setCanceledAction();

        public new void SetResult(T result)
        {
            _tokenRegistration.Dispose();
            base.SetResult(result);
        }

        public new void SetException(Exception exception)
        {
            _tokenRegistration.Dispose();
            base.SetException(exception);
        }

#pragma warning disable IDE0060 // Remove unused parameter
        // Just making sure we don't accidentally call one of these without knowing
#if NETCOREAPP3_0_OR_GREATER
        public static new void SetCanceled(CancellationToken cancellationToken) => Debug.Assert(false);
#else
        public static void SetCanceled(CancellationToken cancellationToken) => Debug.Assert(false);
#endif

        public static new void SetException(IEnumerable<Exception> exceptions) => Debug.Assert(false);
        public static new bool TrySetCanceled()
        {
            Debug.Assert(false);
            return false;
        }
        public static new bool TrySetCanceled(CancellationToken cancellationToken)
        {
            Debug.Assert(false);
            return false;
        }
        public static new bool TrySetException(IEnumerable<Exception> exceptions)
        {
            Debug.Assert(false);
            return false;
        }
        public static new bool TrySetException(Exception exception)
        {
            Debug.Assert(false);
            return false;
        }
        public static new bool TrySetResult(T result)
        {
            Debug.Assert(false);
            return false;
        }
#pragma warning restore IDE0060 // Remove unused parameter
    }
}