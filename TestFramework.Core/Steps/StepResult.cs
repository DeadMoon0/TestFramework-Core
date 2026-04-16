using System;
using TestFramework.Core;

namespace TestFramework.Core.Steps;

public enum StepState
{
    NotRun,
    Complete,
    Timeout,
    Error,
    Skipped
}

public class StepResult<TResult> : StepResultGeneric
{
    public new TResult? Result { get => (TResult?)base.Result; set => base.Result = value!; }
}

public class StepResultGeneric : IFreezable
{
    public bool IsFrozen { get; private set; }
    public void Freeze() { IsFrozen = true; }

    private object? _result = null;
    public object? Result { get => _result; set { ((IFreezable)this).EnsureNotFrozen(); _result = value; } }

    private StepState _state = StepState.NotRun;
    public StepState State { get => _state; set { ((IFreezable)this).EnsureNotFrozen(); _state = value; } }

    private Exception? _exception = null;
    public Exception? Exception { get => _exception; set { ((IFreezable)this).EnsureNotFrozen(); _exception = value; } }
}