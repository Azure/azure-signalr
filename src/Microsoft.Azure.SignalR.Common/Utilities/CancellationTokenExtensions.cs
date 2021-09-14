// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    /// <summary>
    /// From https://github.com/dotnet/corefx/issues/2704#issuecomment-162370041
    /// </summary>
    public static class CancellationTokenExtensions
    {
        public static async Task AsTask(this CancellationToken cancellationToken)
        {
            await cancellationToken;
        }

        public static CancellationTokenAwaiter GetAwaiter(this CancellationToken cancellationToken)
        {
            return new CancellationTokenAwaiter(cancellationToken);
        }

        public class CancellationTokenAwaiter : INotifyCompletion
        {
            private readonly CancellationToken _cancellationToken;

            public bool IsCompleted
            {
                get
                {
                    return _cancellationToken.IsCancellationRequested;
                }
            }

            public CancellationTokenAwaiter(CancellationToken cancellationToken)
            {
                _cancellationToken = cancellationToken;
            }

            public void GetResult()
            {
            }

            public void OnCompleted(Action action)
            {
                _cancellationToken.Register(action);
            }
        }
    }
}
