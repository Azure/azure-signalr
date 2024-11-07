// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Threading.Tasks;

internal static class TaskExtenstions
{
    public static Task<Task> OrSilentCancelAsync(this Task task, CancellationToken token)
    {
        var tcs = new TaskCompletionSource<object>();
        token.Register(() => tcs.TrySetResult(null));

        return Task.WhenAny(task, tcs.Task);
    }
}
