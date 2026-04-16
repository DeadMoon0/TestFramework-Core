namespace TestFramework.Core;

public interface IFreezable
{
    bool IsFrozen { get; }
    void Freeze();
    void EnsureNotFrozen()
    {
        if (IsFrozen) throw new System.InvalidOperationException("This instance has been frozen and is read-only.");
    }
}