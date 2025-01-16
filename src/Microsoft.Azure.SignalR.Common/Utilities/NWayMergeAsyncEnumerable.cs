// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;

namespace Microsoft.Azure.SignalR.Common;

public class NWayMergeAsyncEnumerable<T> : IAsyncEnumerable<T>
{
    private readonly IComparer<T> _comparer;
    private readonly IAsyncEnumerable<T>[] _sources;

    public NWayMergeAsyncEnumerable(params IAsyncEnumerable<T>[] sources)
        : this(null, sources)
    {
    }

    public NWayMergeAsyncEnumerable(IComparer<T> comparer, params IAsyncEnumerable<T>[] sources)
    {
        _comparer = comparer ?? Comparer<T>.Default;
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        foreach (var source in _sources)
        {
            if (source == null)
            {
                throw new ArgumentException("Item cannot be null.", nameof(sources));
            }
        }
    }

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        var sources = Array.ConvertAll(_sources, source => source.GetAsyncEnumerator(cancellationToken));
        var hasMore = new bool[sources.Length];
        for (int i = 0; i < sources.Length; i++)
        {
            hasMore[i] = await sources[i].MoveNextAsync();
        }
        while (Array.IndexOf(hasMore, true) != -1)
        {
            for (int i = 0; i < sources.Length; i++)
            {
                if (!hasMore[i])
                {
                    continue;
                }
                var current = i;
                for (int j = 0; j < sources.Length; j++)
                {
                    if (j != current && hasMore[j] && _comparer.Compare(sources[j].Current, sources[current].Current) < 0)
                    {
                        current = j;
                    }
                }
                yield return sources[current].Current;
                hasMore[current] = await sources[current].MoveNextAsync();
                break;
            }
        }
    }
}