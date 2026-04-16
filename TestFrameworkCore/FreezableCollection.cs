using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace TestFrameworkCore;

internal class FreezableCollection<T> : IFreezableCollection<T>
{
    private readonly ICollection<T> _collection = [];

    public int Count => _collection.Count;

    public bool IsReadOnly => IsFrozen;

    public bool IsFrozen { get; private set; }

    internal FreezableCollection() { }
    private FreezableCollection(ICollection<T> collection)
    {
        _collection = [.. collection];
    }

    public void Add(T item)
    {
        ((IFreezable)this).EnsureNotFrozen();
        _collection.Add(item);
    }

    public IFreezableCollection<TNew> Cast<TNew>()
    {
        return new FreezableCollection<TNew>([.. _collection.Cast<TNew>()])
        {
            IsFrozen = IsFrozen,
        };
    }

    public void Clear()
    {
        ((IFreezable)this).EnsureNotFrozen();
        _collection.Clear();
    }

    public bool Contains(T item)
    {
        ((IFreezable)this).EnsureNotFrozen();
        return _collection.Contains(item);
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        _collection.CopyTo(array, arrayIndex);
    }

    public void Freeze()
    {
        IsFrozen = true;
        if (default(T) is IFreezable)
        {
            foreach (IFreezable? item in _collection)
            {
                if (item is null) continue;
                item.Freeze();
            }
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        return _collection.GetEnumerator();
    }

    public bool Remove(T item)
    {
        ((IFreezable)this).EnsureNotFrozen();
        return _collection.Remove(item);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
