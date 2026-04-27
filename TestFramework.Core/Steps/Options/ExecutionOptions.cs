using TestFramework.Core;

namespace TestFramework.Core.Steps.Options;

/// <summary>
/// Configures how a step is scheduled during execution.
/// </summary>
public class ExecutionOptions : IFreezable
{
    /// <summary>
    /// Gets a value indicating whether the options object has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Freezes the options object.
    /// </summary>
    public void Freeze() { IsFrozen = true; }

    private bool _runExclusively = false;

    /// <summary>
    /// When true, this step must not run concurrently with any other step.
    /// </summary>
    public bool RunExclusively { get => _runExclusively; set { ((IFreezable)this).EnsureNotFrozen(); _runExclusively = value; } }

    /// <summary>
    /// Copies the current options to another instance.
    /// </summary>
    /// <param name="target">The target options instance.</param>
    public void CloneTo(ExecutionOptions target)
    {
        target.RunExclusively = RunExclusively;
    }
}
