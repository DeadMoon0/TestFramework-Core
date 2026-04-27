using System.Collections.Generic;

namespace TestFramework.Core;

/// <summary>
/// Represents a mutable collection that can be frozen into a read-only state.
/// </summary>
/// <typeparam name="T">The item type stored in the collection.</typeparam>
public interface IFreezableCollection<T> : ICollection<T>, IFreezable
{
    /// <summary>
    /// Casts the collection to another item type while preserving the freezable contract.
    /// </summary>
    /// <typeparam name="TNew">The target item type.</typeparam>
    IFreezableCollection<TNew> Cast<TNew>();
}