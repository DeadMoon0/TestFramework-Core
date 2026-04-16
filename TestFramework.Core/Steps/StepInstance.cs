using System.Linq;

namespace TestFramework.Core.Steps;

public class StepInstance<TStep, TResult> : StepInstanceGeneric where TStep : Step<TResult>
{
    public new IFreezableCollection<StepResult<TResult>> RetryResults { get => base.RetryResults.Cast<StepResult<TResult>>(); }
    public new StepResult<TResult> LastResult { get => (StepResult<TResult>)base.RetryResults.Last(); }

    public StepInstance(Step<TResult> step) : base(step) { }
}

public class StepInstanceGeneric : IFreezable
{
    public bool IsFrozen { get; private set; }
    public void Freeze()
    {
        RetryResults.Freeze();
        IsFrozen = true;
    }

    public StepGeneric Step { get; }

    public IFreezableCollection<StepResultGeneric> RetryResults { get; } = new FreezableCollection<StepResultGeneric>();
    public StepResultGeneric LastResult { get => RetryResults.Last(); }
    public StepState State { get => LastResult.State; }

    public StepInstanceGeneric(StepGeneric step) { Step = step; }
}