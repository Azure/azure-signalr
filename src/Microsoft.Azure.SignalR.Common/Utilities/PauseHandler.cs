// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR;

internal class PauseHandler
{
    private readonly SemaphoreSlim _pauseSemaphore = new(1, 1);

    private volatile int _paused;

    private volatile int _pauseAcked = 1;

    public bool ShouldReplyAck => Interlocked.CompareExchange(ref _pauseAcked, 1, 0) == 0;

    public async Task<bool> WaitAsync(int ms, CancellationToken ctoken) => _pauseSemaphore.Wait(0, ctoken) || await _pauseSemaphore.WaitAsync(ms, ctoken);

    public void Release() => _pauseSemaphore.Release();

    public Task PauseAsync()
    {
        if (Interlocked.CompareExchange(ref _paused, 1, 0) == 0)
        {
            Interlocked.Exchange(ref _pauseAcked, 0);
            return _pauseSemaphore.WaitAsync();
        }
        return Task.CompletedTask;
    }

    public Task ResumeAsync()
    {
        if (Interlocked.CompareExchange(ref _paused, 0, 1) == 1)
        {
            _pauseSemaphore.Release();
        }
        return Task.CompletedTask;
    }
}
