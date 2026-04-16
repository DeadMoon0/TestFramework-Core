using System.Collections.Generic;

namespace TestFrameworkCore;

public interface IFreezableDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IFreezable
{
}