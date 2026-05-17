namespace TestFramework.Core.Steps.Options;

/// <summary>
/// Controls how a step may be scheduled relative to other steps in the same stage.
/// </summary>
public enum StepParallelizationMode
{
    /// <summary>
    /// The step may run in parallel with other non-conflicting steps.
    /// </summary>
    Parallelizable,

    /// <summary>
    /// The step must not run in parallel with any other step.
    /// </summary>
    DoNotParallelize
}