using System.Collections.Generic;

namespace TestFramework.Core;

public interface IFreezableCollection<T> : ICollection<T>, IFreezable
{
    IFreezableCollection<TNew> Cast<TNew>();
}