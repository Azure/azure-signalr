using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

#nullable enable

namespace Microsoft.Azure.SignalR.Protocol
{

    /// <summary>
    /// Lightweight, read-only IDictionary implementation using two arrays
    /// and O(n) lookup.
    /// Requires specifying capacity at construction and does not
    /// support reallocation to increase capacity.
    /// ref: https://github.com/dotnet/dotnet/blob/main/src/msbuild/src/Build/Collections/ArrayDictionary.cs
    /// </summary>
    /// <typeparam name="TKey">Type of keys</typeparam>
    /// <typeparam name="TValue">Type of values</typeparam>
    public class ArrayDictionary<TKey, TValue>
        : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
        where TKey : notnull
    {
        public static readonly ArrayDictionary<TKey, TValue> Empty = new(0);

        private readonly IEqualityComparer<TKey> _comparer;
        private readonly TKey[] _keys;
        private readonly TValue[] _values;

        private int count;

        public ArrayDictionary(int capacity, IEqualityComparer<TKey>? comparer = null)
        {
            _keys = new TKey[capacity];
            _values = new TValue[capacity];
            _comparer = comparer ?? EqualityComparer<TKey>.Default;
        }

        public TValue this[TKey key]
        {
            get
            {
                if (TryGetValue(key, out var value))
                {
                    return value;
                }
                throw new KeyNotFoundException();
            }
            set
            {
                for (int i = 0; i < count; i++)
                {
                    if (_comparer.Equals(key, _keys[i]))
                    {
                        _values[i] = value;
                        return;
                    }
                }
                Add(key, value);
            }
        }

        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => _keys;

        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => _values;

        private IEqualityComparer<TKey> KeyComparer => _comparer;

        private IEqualityComparer<TValue> ValueComparer => EqualityComparer<TValue>.Default;

        public int Count => count;

        public bool IsReadOnly => true;

        public ICollection<TKey> Keys => _keys;

        public ICollection<TValue> Values => _values;

        public void Add(TKey key, TValue value)
        {
            if (count < _keys.Length)
            {
                _keys[count] = key;
                _values[count] = value;
                count += 1;
            }
            else
            {
                throw new InvalidOperationException($"ArrayDictionary is at capacity {_keys.Length}");
            }
        }

        public void Add(KeyValuePair<TKey, TValue> item) =>
            Add(item.Key, item.Value);

        public void Clear() => throw new System.NotImplementedException();

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            var valueComparer = ValueComparer;
            for (int i = 0; i < count; i++)
            {
                if (_comparer.Equals(item.Key, _keys[i]) && valueComparer.Equals(item.Value, _values[i]))
                {
                    return true;
                }
            }
            return false;
        }

        public bool ContainsKey(TKey key)
        {
            for (int i = 0; i < count; i++)
            {
                if (_comparer.Equals(key, _keys[i]))
                {
                    return true;
                }
            }
            return false;
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            for (int i = 0; i < count; i++)
            {
                array[arrayIndex + i] = new(_keys[i], _values[i]);
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() =>
            new Enumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool Remove(TKey key) =>
            throw new System.NotImplementedException();

        public bool Remove(KeyValuePair<TKey, TValue> item) =>
            throw new System.NotImplementedException();

        public bool TryGetValue(TKey key, out TValue value)
        {
            for (int i = 0; i < count; i++)
            {
                if (_comparer.Equals(key, _keys[i]))
                {
                    value = _values[i];
                    return true;
                }
            }
            value = default!;
            return false;
        }

        private struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
        {
            private readonly ArrayDictionary<TKey, TValue> _dictionary;
            private int _position;

            public Enumerator(ArrayDictionary<TKey, TValue> dictionary)
            {
                this._dictionary = dictionary;
                this._position = -1;
            }

            public KeyValuePair<TKey, TValue> Current =>
                new KeyValuePair<TKey, TValue>(
                    _dictionary._keys[_position],
                    _dictionary._values[_position]);

            object IEnumerator.Current => Current;

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                _position += 1;
                return _position < _dictionary.Count;
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }
        }
    }
}