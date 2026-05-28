namespace TestFramework.Core.Steps.Options;

/// <summary>
/// Controls how a step may be scheduled relative to other steps in the same stage.
/// </summary>
public enum StepParallelizationMode
{
    /// <summary>
    /// The step may run in parallel with other non-conflicting steps when the planner allows that phase to merge.
    /// In the built-in planner, authored steps in <see cref="StepExecutionPhase.Prepare"/> and <see cref="StepExecutionPhase.Materialize"/> are mergeable;
    /// <see cref="StepExecutionPhase.Act"/> and <see cref="StepExecutionPhase.Observe"/> remain sequential even when marked parallelizable.
    /// </summary>
    Parallelizable,

    /// <summary>
    /// The step must not run in parallel with any other step.
    /// </summary>
    DoNotParallelize
}