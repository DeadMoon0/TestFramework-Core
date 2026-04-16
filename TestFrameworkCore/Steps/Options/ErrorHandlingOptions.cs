using System;

namespace TestFrameworkCore.Steps.Options;

public class ErrorHandlingOptions : IFreezable
{
    public bool IsFrozen { get; private set; }
    public void Freeze()
    {
        IsFrozen = true;
        IgnoreExceptionTypes.Freeze();
    }

    public IFreezableCollection<Type> IgnoreExceptionTypes { get; } = new FreezableCollection<Type>();

    public void CloneTo(ErrorHandlingOptions target)
    {
        target.IgnoreExceptionTypes.Clear();
        foreach (var item in IgnoreExceptionTypes)
        {
            target.IgnoreExceptionTypes.Add(item);
        }
    }
}