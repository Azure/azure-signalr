// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests;

public class NWayMergeAsyncEnumerableTest
{
    [Fact]
    public async Task TestOneWayMergeAsyncEnumerable()
    {
        List<int> source = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        var sources = new IAsyncEnumerable<int>[]
        {
            ToAsyncEnumerable(source),
        };
        var multiWayMergeAsyncEnumerable = new NWayMergeAsyncEnumerable<int>(sources);
        var result = await ToListAsync(multiWayMergeAsyncEnumerable);
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8, 9, 10], result);
    }

    [Fact]
    public async Task TestEmptyOneWayMergeAsyncEnumerable()
    {
        List<int> source = [];
        var sources = new IAsyncEnumerable<int>[]
        {
            ToAsyncEnumerable(source),
        };
        var multiWayMergeAsyncEnumerable = new NWayMergeAsyncEnumerable<int>(sources);
        var result = await ToListAsync(multiWayMergeAsyncEnumerable);
        Assert.Equal([], result);
    }

    [Fact]
    public async Task TestThreeWayMergeAsyncEnumerable()
    {
        List<int> source1 = [1, 3, 5, 7, 9];
        List<int> source2 = [2, 4, 6, 8, 10];
        List<int> source3 = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        var sources = new IAsyncEnumerable<int>[]
        {
            ToAsyncEnumerable(source1),
            ToAsyncEnumerable(source2),
            ToAsyncEnumerable(source3),
        };
        var multiWayMergeAsyncEnumerable = new NWayMergeAsyncEnumerable<int>(sources);
        var result = await ToListAsync(multiWayMergeAsyncEnumerable);
        Assert.Equal([1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10], result);
    }

    [Fact]
    public async Task TestEmptyThreeWayMergeAsyncEnumerable()
    {
        List<int> source1 = [];
        List<int> source2 = [];
        List<int> source3 = [];
        var sources = new IAsyncEnumerable<int>[]
        {
            ToAsyncEnumerable(source1),
            ToAsyncEnumerable(source2),
            ToAsyncEnumerable(source3),
        };
        var multiWayMergeAsyncEnumerable = new NWayMergeAsyncEnumerable<int>(sources);
        var result = await ToListAsync(multiWayMergeAsyncEnumerable);
        Assert.Equal([], result);
    }

    [Fact]
    public void TestNullCases()
    {
        IAsyncEnumerable<int>[] args = null;
        Assert.Throws<ArgumentNullException>(() => new NWayMergeAsyncEnumerable<int>(args));
        IAsyncEnumerable<int> arg = null;
        Assert.Throws<ArgumentException>(() => new NWayMergeAsyncEnumerable<int>(arg));
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            await Task.Delay(1);
            yield return item;
        }
    }

    private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
        {
            list.Add(item);
        }
        return list;
    }
}
