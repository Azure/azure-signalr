// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Common.Tests;
public class MockAsyncEnumerable<T>(IEnumerable<T> values) : IAsyncEnumerable<T>
{
    public static IAsyncEnumerable<Tp> From<Tp>(params Tp[] items)
    {
        return new MockAsyncEnumerable<Tp>(items);
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new MockAsyncEnumerator<T>(values.GetEnumerator());
    }

    private sealed class MockAsyncEnumerator<Tp>(IEnumerator<Tp> enumerator) : IAsyncEnumerator<Tp>
    {
        public Tp Current => enumerator.Current;

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> MoveNextAsync()
        {
            return ValueTask.FromResult(enumerator.MoveNext());
        }
    }
}