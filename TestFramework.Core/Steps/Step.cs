using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Steps;

#pragma warning disable CA1716 // Type names should not match keywords
public abstract class Step<TResult> : StepGeneric
#pragma warning restore CA1716
{
    public abstract Task<TResult?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken);
    public abstract StepInstance<Step<TResult>, TResult> GetInstance();
    public abstract Step<TResult> Clone();

    public TStep WithClonedOptions<TStep>(TStep from) where TStep : Step<TResult> => (TStep)base.WithClonedOptions(from);

    public override async Task<object?> ExecuteGeneric(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken) => await Execute(serviceProvider, variableStore, artifactStore, logger, cancellationToken);
    public override StepInstanceGeneric GetInstanceGeneric() => GetInstance();
    public override StepGeneric CloneGeneric() => Clone();
}

public abstract class StepGeneric : IFreezable
{
    public bool IsFrozen { get; private set; }
    public void Freeze()
    {
        IsFrozen = true;
        RetryOptions.Freeze();
        ErrorHandlingOptions.Freeze();
        TimeOutOptions.Freeze();
        ExecutionOptions.Freeze();
        IOContract.Freeze();
    }

    public abstract string Name { get; }
    public abstract string Description { get; }

    public RetryOptions RetryOptions { get; init; } = new RetryOptions();
    public ErrorHandlingOptions ErrorHandlingOptions { get; init; } = new ErrorHandlingOptions();
    public TimeOutOptions TimeOutOptions { get; init; } = new TimeOutOptions();
    public LabelOptions LabelOptions { get; init; } = new LabelOptions();
    public ExecutionOptions ExecutionOptions { get; init; } = new ExecutionOptions();
    public StepIOContract IOContract { get; init; } = new StepIOContract();

    public abstract bool DoesReturn { get; }

    public abstract Task<object?> ExecuteGeneric(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken);
    public abstract void DeclareIO(StepIOContract contract);

    public abstract StepInstanceGeneric GetInstanceGeneric();

    public abstract StepGeneric CloneGeneric();
    public StepGeneric WithClonedOptions(StepGeneric from)
    {
        from.RetryOptions.CloneTo(RetryOptions);
        from.ErrorHandlingOptions.CloneTo(ErrorHandlingOptions);
        from.TimeOutOptions.CloneTo(TimeOutOptions);
        from.LabelOptions.CloneTo(LabelOptions);
        from.ExecutionOptions.CloneTo(ExecutionOptions);
        from.IOContract.CloneTo(IOContract);
        return this;
    }
}