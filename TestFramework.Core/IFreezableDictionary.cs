using System.Collections.Generic;

namespace TestFramework.Core;

/// <summary>
/// Represents a mutable dictionary that can be frozen into a read-only state.
/// </summary>
/// <typeparam name="TKey">The dictionary key type.</typeparam>
/// <typeparam name="TValue">The dictionary value type.</typeparam>
public interface IFreezableDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IFreezable
{
}