using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace TestFrameworkCore;

internal class FreezableDictionary<TKey, TValue> : IFreezableDictionary<TKey, TValue> where TKey : notnull
{
    public bool IsFrozen { get; private set; }
    public void Freeze()
    {
        IsFrozen = true;
        if (default(TValue) is IFreezable)
        {
            foreach (IFreezable? item in _dictionary.Values)
            {
                if (item is null) continue;
                item.Freeze();
            }
        }
    }

    private readonly Dictionary<TKey, TValue> _dictionary = [];

    public TValue this[TKey key] { get => _dictionary[key]; set { ((IFreezable)this).EnsureNotFrozen(); _dictionary[key] = value; } }

    public ICollection<TKey> Keys => _dictionary.Keys;

    public ICollection<TValue> Values => _dictionary.Values;

    public int Count => _dictionary.Count;

    public bool IsReadOnly => IsFrozen;

    public void Add(TKey key, TValue value)
    {
        ((IFreezable)this).EnsureNotFrozen();
        _dictionary.Add(key, value);
    }

    public void Add(KeyValuePair<TKey, TValue> item)
    {
        ((IFreezable)this).EnsureNotFrozen();
        _dictionary.Add(item.Key, item.Value);
    }

    public void Clear()
    {
        ((IFreezable)this).EnsureNotFrozen();
        _dictionary.Clear();
    }

    public bool Contains(KeyValuePair<TKey, TValue> item)
    {
        return _dictionary.Contains(item);
    }

    public bool ContainsKey(TKey key)
    {
        return _dictionary.ContainsKey(key);
    }

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        ((IDictionary<TKey, TValue>)_dictionary).CopyTo(array, arrayIndex);
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        return _dictionary.GetEnumerator();
    }

    public bool Remove(TKey key)
    {
        ((IFreezable)this).EnsureNotFrozen();
        return _dictionary.Remove(key);
    }

    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
        ((IFreezable)this).EnsureNotFrozen();
        return ((IDictionary<TKey, TValue>)_dictionary).Remove(item);
    }

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        return _dictionary.TryGetValue(key, out value);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
