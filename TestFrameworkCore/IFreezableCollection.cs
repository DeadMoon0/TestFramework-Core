using System.Collections.Generic;

namespace TestFrameworkCore;

public interface IFreezableCollection<T> : ICollection<T>, IFreezable
{
    IFreezableCollection<TNew> Cast<TNew>();
}