// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Microsoft.Azure.SignalR.Emulator.HubEmulator
{
    internal class ManyToManyMap<TLeft, TRight>
        where TLeft : class
        where TRight : class
    {
        private readonly IEqualityComparer<TLeft> _leftComparer;
        private readonly IEqualityComparer<TRight> _rightComparer;
        private readonly ConcurrentDictionary<TLeft, Set<TRight>> _ltr;
        private readonly ConcurrentDictionary<TRight, Set<TLeft>> _rtl;

        public ManyToManyMap()
           : this(EqualityComparer<TLeft>.Default, EqualityComparer<TRight>.Default)
        {
        }

        public ManyToManyMap(IEqualityComparer<TLeft> leftComparer, IEqualityComparer<TRight> rightComparer)
        {
            _leftComparer = leftComparer;
            _rightComparer = rightComparer;
            _ltr = new ConcurrentDictionary<TLeft, Set<TRight>>(leftComparer);
            _rtl = new ConcurrentDictionary<TRight, Set<TLeft>>(rightComparer);
        }

        public ICollection<TLeft> Lefts => _ltr.Keys;

        public ICollection<TRight> Rights => _rtl.Keys;

        public void Add(TLeft left, TRight right)
        {
            _ltr.AddOrUpdate(left, new Set<TRight>(_rightComparer) { right }, (_, items) => { lock (items) { items.Add(right); } return items; });
            _rtl.AddOrUpdate(right, new Set<TLeft>(_leftComparer) { left }, (_, items) => { lock (items) { items.Add(left); } return items; });
        }

        public void Remove(TLeft left, TRight right)
        {
            RemoveLeftFromRight(left, right);
            RemoveRightFromLeft(left, right);
        }

        public void RemoveRightFromLeft(TLeft left, TRight right)
        {
            if (_ltr.TryGetValue(left, out var lefts))
            {
                lock (lefts)
                {
                    lefts.Remove(right);
                    if (lefts.Count == 0)
                    {
                        ((ICollection<KeyValuePair<TLeft, Set<TRight>>>)_ltr).Remove(KeyValuePair.Create(left, lefts));
                    }
                }
            }
        }

        public void RemoveLeftFromRight(TLeft left, TRight right)
        {
            if (_rtl.TryGetValue(right, out var rights))
            {
                lock (rights)
                {
                    rights.Remove(left);
                    if (rights.Count == 0)
                    {
                        ((ICollection<KeyValuePair<TRight, Set<TLeft>>>)_rtl).Remove(KeyValuePair.Create(right, rights));
                    }
                }
            }
        }

        public void RemoveLeft(TLeft left)
        {
            _ltr.Remove(left, out var lefts);

            foreach (var right in lefts)
            {
                RemoveLeftFromRight(left, right);
            }
        }

        public LeaseForArray<TRight> QueryByLeft(TLeft left)
        {
            if (_ltr.TryGetValue(left, out var set))
            {
                lock (set)
                {
                    if (set.Count == 0)
                    {
                        return LeaseForArray<TRight>.Empty;
                    }
                    var array = ArrayPool<TRight>.Shared.Rent(set.Count);
                    set.CopyTo(array);
                    return LeaseForArray.Create(array, set.Count);
                }
            }
            return LeaseForArray<TRight>.Empty;
        }

        public bool RightExists(TRight right)
        {
            if (_rtl.TryGetValue(right, out var set))
            {
                lock (set)
                {
                    if (set.Count == 0)
                    {
                        return false;
                    }

                    return true;
                }
            }
            return false;
        }

        public bool Exists(TRight right, TLeft left)
        {
            if (_rtl.TryGetValue(right, out var set))
            {
                lock (set)
                {
                    if (set.Contains(left))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public LeaseForArray<TLeft> QueryByRight(TRight right)
        {
            if (_rtl.TryGetValue(right, out var set))
            {
                lock (set)
                {
                    if (set.Count == 0)
                    {
                        return LeaseForArray<TLeft>.Empty;
                    }
                    var array = ArrayPool<TLeft>.Shared.Rent(set.Count);
                    set.CopyTo(array);
                    return LeaseForArray.Create(array, set.Count);
                }
            }
            return LeaseForArray<TLeft>.Empty;
        }

        private sealed class Set<T> : HashSet<T>
        {
            public Set(IEqualityComparer<T> comparer)
                : base(comparer)
            {
            }

            public override int GetHashCode() => Count;

            public override bool Equals(object obj) => Count == (obj as Set<T>)?.Count;
        }
    }
}
