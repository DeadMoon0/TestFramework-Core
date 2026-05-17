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

    private StepParallelizationMode _parallelizationMode = StepParallelizationMode.Parallelizable;

    /// <summary>
    /// Controls whether the step may run in parallel with other steps.
    /// </summary>
    public StepParallelizationMode ParallelizationMode { get => _parallelizationMode; set { ((IFreezable)this).EnsureNotFrozen(); _parallelizationMode = value; } }

    /// <summary>
    /// Copies the current options to another instance.
    /// </summary>
    /// <param name="target">The target options instance.</param>
    public void CloneTo(ExecutionOptions target)
    {
        target.ParallelizationMode = ParallelizationMode;
    }
}
