using System.Collections.Generic;
using System.Linq;

namespace TestFramework.Core.Steps;

/// <summary>
/// Represents a typed runtime instance of a step and its retry results.
/// </summary>
/// <typeparam name="TStep">The concrete step type.</typeparam>
/// <typeparam name="TStepResultContext">The step result context type.</typeparam>
public class StepInstance<TStep, TStepResultContext> : StepInstanceGeneric where TStep : Step<TStepResultContext> where TStepResultContext : StepResultContext
{
    /// <summary>
    /// Gets the typed retry results recorded for the step instance.
    /// </summary>
    public new IReadOnlyList<StepResult<TStepResultContext>> RetryResults => [.. base.RetryResults.Cast<StepResult<TStepResultContext>>()];

    /// <summary>
    /// Gets the last typed retry result recorded for the step instance.
    /// </summary>
    /// <exception cref="Exceptions.FrameworkStateException">The step has no result yet, because it has not run.</exception>
    public new StepResult<TStepResultContext> LastResult => (StepResult<TStepResultContext>)base.LastResult;

    /// <summary>
    /// Initializes a new typed step instance for the provided step definition.
    /// </summary>
    /// <param name="step">The step definition represented by this runtime instance.</param>
    public StepInstance(Step<TStepResultContext> step) : base(step) { }
}

/// <summary>
/// Represents a runtime instance of a step and the results recorded across its executions and retries.
/// </summary>
public class StepInstanceGeneric
{
    /// <summary>
    /// Gets a value indicating whether the step instance has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Freezes the step instance and its retry results against further mutation.
    /// </summary>
    internal void Freeze()
    {
        _retryResults.Freeze();
        foreach (StepResultGeneric attempt in _retryResults)
            attempt.Freeze();

        IsFrozen = true;
    }

    /// <summary>
    /// Gets the step definition represented by this runtime instance.
    /// </summary>
    public StepGeneric Step { get; }

    /// <summary>
    /// Gets the recorded results for each execution attempt of the step.
    /// </summary>
    /// <remarks>
    /// Read-only to everyone but the runner: a result is recorded by the engine that produced it, and a
    /// collection that let a reader add one was a second road into a finished run.
    /// </remarks>
    public IReadOnlyList<StepResultGeneric> RetryResults => _retryResults;

    private readonly FreezableCollection<StepResultGeneric> _retryResults = new();

    /// <summary>
    /// Records the result of one attempt. Refused once the step is frozen.
    /// </summary>
    internal void AddResult(StepResultGeneric result) => _retryResults.Add(result);

    /// <summary>
    /// Gets the last recorded execution result for the step.
    /// </summary>
    /// <exception cref="Exceptions.FrameworkStateException">The step has no result yet, because it has not run.</exception>
    public StepResultGeneric LastResult => RetryResults.Count > 0
        ? RetryResults.Last()
        : throw new Exceptions.FrameworkStateException(
            $"Step '{Step.LabelOptions.Label ?? Step.Name}' has no result yet, because it has not run.")
        {
            RecoverySteps = ["Read State instead: it answers NotRun for a step that has not run.", "After a run, every step has a result - a step the run never reached is Skipped, and its SkipReason says why."],
        };

    /// <summary>
    /// Gets the current state of the step: the state of its last attempt, or <see cref="StepState.NotRun"/>
    /// when it has none.
    /// </summary>
    public StepState State => RetryResults.Count > 0 ? RetryResults.Last().State : StepState.NotRun;

    /// <summary>
    /// Initializes a new runtime step instance for the provided step definition.
    /// </summary>
    /// <param name="step">The step definition represented by this runtime instance.</param>
    public StepInstanceGeneric(StepGeneric step) { Step = step; }
}