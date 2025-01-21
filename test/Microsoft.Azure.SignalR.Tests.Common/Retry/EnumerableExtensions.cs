// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Azure.SignalR.Tests;

#nullable enable

public static class EnumerableExtensions
{
    public static bool ContainsAny<T>(this IEnumerable<T> values, T[] searchFor, IEqualityComparer<T>? comparer = null)
    {
        if (searchFor == null)
        {
            throw new ArgumentNullException(nameof(searchFor));
        }

        comparer ??= EqualityComparer<T>.Default;

        return searchFor.Length != 0 &&
               values.Any(val => searchFor.Any(search => comparer.Equals(val, search)));
    }
}
