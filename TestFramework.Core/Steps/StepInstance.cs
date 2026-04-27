using System.Linq;

namespace TestFramework.Core.Steps;

/// <summary>
/// Represents a typed runtime instance of a step and its retry results.
/// </summary>
/// <typeparam name="TStep">The concrete step type.</typeparam>
/// <typeparam name="TResult">The step result value type.</typeparam>
public class StepInstance<TStep, TResult> : StepInstanceGeneric where TStep : Step<TResult>
{
    /// <summary>
    /// Gets the typed retry results recorded for the step instance.
    /// </summary>
    public new IFreezableCollection<StepResult<TResult>> RetryResults { get => base.RetryResults.Cast<StepResult<TResult>>(); }

    /// <summary>
    /// Gets the last typed retry result recorded for the step instance.
    /// </summary>
    public new StepResult<TResult> LastResult { get => (StepResult<TResult>)base.RetryResults.Last(); }

    /// <summary>
    /// Initializes a new typed step instance for the provided step definition.
    /// </summary>
    /// <param name="step">The step definition represented by this runtime instance.</param>
    public StepInstance(Step<TResult> step) : base(step) { }
}

/// <summary>
/// Represents a runtime instance of a step and the results recorded across its executions and retries.
/// </summary>
public class StepInstanceGeneric : IFreezable
{
    /// <summary>
    /// Gets a value indicating whether the step instance has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Freezes the step instance and its retry results against further mutation.
    /// </summary>
    public void Freeze()
    {
        RetryResults.Freeze();
        IsFrozen = true;
    }

    /// <summary>
    /// Gets the step definition represented by this runtime instance.
    /// </summary>
    public StepGeneric Step { get; }

    /// <summary>
    /// Gets the recorded results for each execution attempt of the step.
    /// </summary>
    public IFreezableCollection<StepResultGeneric> RetryResults { get; } = new FreezableCollection<StepResultGeneric>();

    /// <summary>
    /// Gets the last recorded execution result for the step.
    /// </summary>
    public StepResultGeneric LastResult { get => RetryResults.Last(); }

    /// <summary>
    /// Gets the current state of the step based on its last recorded result.
    /// </summary>
    public StepState State { get => LastResult.State; }

    /// <summary>
    /// Initializes a new runtime step instance for the provided step definition.
    /// </summary>
    /// <param name="step">The step definition represented by this runtime instance.</param>
    public StepInstanceGeneric(StepGeneric step) { Step = step; }
}