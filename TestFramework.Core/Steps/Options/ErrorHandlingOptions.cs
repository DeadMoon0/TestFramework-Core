using System;

namespace TestFramework.Core.Steps.Options;

/// <summary>
/// Configures how a step handles exceptions during execution.
/// </summary>
public class ErrorHandlingOptions : IFreezable
{
    /// <summary>
    /// Gets a value indicating whether the options object has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Freezes the options object and its ignored-exception collection.
    /// </summary>
    public void Freeze()
    {
        IsFrozen = true;
        IgnoreExceptionTypes.Freeze();
    }

    /// <summary>
    /// Gets the exception types that should be ignored by the step.
    /// </summary>
    public IFreezableCollection<Type> IgnoreExceptionTypes { get; } = new FreezableCollection<Type>();

    /// <summary>
    /// Copies the current options to another instance.
    /// </summary>
    /// <param name="target">The target options instance.</param>
    public void CloneTo(ErrorHandlingOptions target)
    {
        target.IgnoreExceptionTypes.Clear();
        foreach (var item in IgnoreExceptionTypes)
        {
            target.IgnoreExceptionTypes.Add(item);
        }
    }
}