// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Threading.Tasks;

#nullable enable

internal static class TaskExtenstions
{
    public static async Task OrCancelAsync(this Task task, CancellationToken token, string? message = null)
    {
        var tcs = new TaskCompletionSource<object?>();
        token.Register(() => tcs.TrySetResult(null));

        var anyTask = await Task.WhenAny(task, tcs.Task);

        if (anyTask == task)
        {
            // make sure the task throws exception if any
            await anyTask;
            tcs.TrySetCanceled();
        }
        else
        {
            throw message == null ? new TaskCanceledException() : new TaskCanceledException(message);
        }
    }
}
