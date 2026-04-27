namespace TestFramework.Core;

/// <summary>
/// Defines an object that can transition into an immutable, read-only state.
/// </summary>
public interface IFreezable
{
    /// <summary>
    /// Gets a value indicating whether the instance has been frozen.
    /// </summary>
    bool IsFrozen { get; }

    /// <summary>
    /// Freezes the instance against further mutation.
    /// </summary>
    void Freeze();

    /// <summary>
    /// Throws if the instance is already frozen.
    /// </summary>
    void EnsureNotFrozen()
    {
        if (IsFrozen) throw new System.InvalidOperationException("This instance has been frozen and is read-only.");
    }
}