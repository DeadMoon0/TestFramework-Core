using System.ComponentModel;
using TestFramework.Core.Steps.Options;

namespace TestFramework.Core.Debugger;

/// <summary>
/// Represents the debugger-facing structure of a step.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public record DebugStepState
{
    /// <summary>
    /// Gets the step name.
    /// </summary>
    public required string Name { get; init; }
    /// <summary>
    /// Gets the step description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the retry options for the step.
    /// </summary>
    public required RetryOptions RetryOptions { get; init; }
    /// <summary>
    /// Gets the error handling options for the step.
    /// </summary>
    public required ErrorHandlingOptions ErrorHandlingOptions { get; init; }
    /// <summary>
    /// Gets the timeout options for the step.
    /// </summary>
    public required TimeOutOptions TimeOutOptions { get; init; }
    /// <summary>
    /// Gets the label options for the step.
    /// </summary>
    public required LabelOptions LabelOptions { get; init; }
    /// <summary>
    /// Gets the execution options for the step.
    /// </summary>
    public required ExecutionOptions ExecutionOptions { get; init; }
    /// <summary>
    /// Gets the declared IO contract for the step.
    /// </summary>
    public required StepIOContract IOContract { get; init; }

    /// <summary>
    /// Gets the workflow phase used by the planner for this step.
    /// </summary>
    public required StepExecutionPhase Phase { get; init; }

    /// <summary>
    /// Gets a value indicating whether the step returns a result.
    /// </summary>
    public required bool DoesReturn { get; init; }

    /// <summary>
    /// Gets the execution layer the planner assigned this step to.
    /// </summary>
    /// <remarks>
    /// Steps sharing a layer run concurrently; a later layer starts only once the one before it has
    /// finished. Reported rather than left for a consumer to work out, because the real plan depends
    /// on things the protocol does not carry — parallelization mode, IO access conflicts, artifact
    /// setup resources shared between steps. A consumer re-deriving it from the IO contract alone
    /// would draw a plan that disagrees with the one that ran, which is worse than drawing none:
    /// the whole reason to show layers is to answer "what actually ran at the same time".
    /// </remarks>
    public int LayerIndex { get; init; }
}