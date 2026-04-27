using System;
using TestFramework.Core;

namespace TestFramework.Core.Steps;

/// <summary>
/// Represents the execution state of a step attempt.
/// </summary>
public enum StepState
{
    /// <summary>
    /// The step has not been executed yet.
    /// </summary>
    NotRun,

    /// <summary>
    /// The step completed successfully.
    /// </summary>
    Complete,

    /// <summary>
    /// The step exceeded its configured timeout.
    /// </summary>
    Timeout,

    /// <summary>
    /// The step ended with an error.
    /// </summary>
    Error,

    /// <summary>
    /// The step was skipped.
    /// </summary>
    Skipped
}

/// <summary>
/// Represents the typed result of a single step attempt.
/// </summary>
/// <typeparam name="TResult">The step result value type.</typeparam>
public class StepResult<TResult> : StepResultGeneric
{
    /// <summary>
    /// Gets or sets the typed result value produced by the step attempt.
    /// </summary>
    public new TResult? Result { get => (TResult?)base.Result; set => base.Result = value!; }
}

/// <summary>
/// Represents the untyped result of a single step attempt, including state and exception details.
/// </summary>
public class StepResultGeneric : IFreezable
{
    /// <summary>
    /// Gets a value indicating whether the step result has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Freezes the step result against further mutation.
    /// </summary>
    public void Freeze() { IsFrozen = true; }

    private object? _result = null;

    /// <summary>
    /// Gets or sets the untyped result value produced by the step attempt.
    /// </summary>
    public object? Result { get => _result; set { ((IFreezable)this).EnsureNotFrozen(); _result = value; } }

    private StepState _state = StepState.NotRun;

    /// <summary>
    /// Gets or sets the execution state of the step attempt.
    /// </summary>
    public StepState State { get => _state; set { ((IFreezable)this).EnsureNotFrozen(); _state = value; } }

    private Exception? _exception = null;

    /// <summary>
    /// Gets or sets the exception captured for the step attempt, when one exists.
    /// </summary>
    public Exception? Exception { get => _exception; set { ((IFreezable)this).EnsureNotFrozen(); _exception = value; } }
}