using System.Collections.Generic;

namespace TestFramework.Core;

public interface IFreezableDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IFreezable
{
}