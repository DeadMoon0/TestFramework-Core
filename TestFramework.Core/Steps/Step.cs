using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Steps;

#pragma warning disable CA1716 // Type names should not match keywords
/// <summary>
/// Represents a typed executable step in a timeline.
/// </summary>
/// <typeparam name="TResult">The result type produced by the step.</typeparam>
public abstract class Step<TResult> : StepGeneric
#pragma warning restore CA1716
{
    /// <summary>
    /// Executes the step.
    /// </summary>
    /// <param name="serviceProvider">The service provider available to the step.</param>
    /// <param name="variableStore">The current run variable store.</param>
    /// <param name="artifactStore">The current run artifact store.</param>
    /// <param name="logger">The scoped logger for the run.</param>
    /// <param name="cancellationToken">The cancellation token for the running step.</param>
    public abstract Task<TResult?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a runtime instance for this step.
    /// </summary>
    public abstract StepInstance<Step<TResult>, TResult> GetInstance();

    /// <summary>
    /// Creates a clone of this step definition.
    /// </summary>
    public abstract Step<TResult> Clone();

    /// <summary>
    /// Copies the option state from another step clone into this instance.
    /// </summary>
    /// <typeparam name="TStep">The concrete step type.</typeparam>
    /// <param name="from">The step whose options should be copied.</param>
    public TStep WithClonedOptions<TStep>(TStep from) where TStep : Step<TResult> => (TStep)base.WithClonedOptions(from);

    /// <summary>
    /// Executes the step through the untyped base contract.
    /// </summary>
    public override async Task<object?> ExecuteGeneric(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken) => await Execute(serviceProvider, variableStore, artifactStore, logger, cancellationToken);

    /// <summary>
    /// Creates an untyped runtime instance for this step.
    /// </summary>
    public override StepInstanceGeneric GetInstanceGeneric() => GetInstance();

    /// <summary>
    /// Creates an untyped clone of this step definition.
    /// </summary>
    public override StepGeneric CloneGeneric() => Clone();
}

/// <summary>
/// Represents the untyped base contract for all executable steps.
/// </summary>
public abstract class StepGeneric : IFreezable
{
    /// <summary>
    /// Gets a value indicating whether the step definition has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Freezes the step definition and its option objects.
    /// </summary>
    public void Freeze()
    {
        IsFrozen = true;
        RetryOptions.Freeze();
        ErrorHandlingOptions.Freeze();
        TimeOutOptions.Freeze();
        ExecutionOptions.Freeze();
        IOContract.Freeze();
    }

    /// <summary>
    /// Gets the display name of the step.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the description of the step.
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    /// Gets the retry configuration for the step.
    /// </summary>
    public RetryOptions RetryOptions { get; init; } = new RetryOptions();

    /// <summary>
    /// Gets the error handling configuration for the step.
    /// </summary>
    public ErrorHandlingOptions ErrorHandlingOptions { get; init; } = new ErrorHandlingOptions();

    /// <summary>
    /// Gets the timeout configuration for the step.
    /// </summary>
    public TimeOutOptions TimeOutOptions { get; init; } = new TimeOutOptions();

    /// <summary>
    /// Gets the label configuration for the step.
    /// </summary>
    public LabelOptions LabelOptions { get; init; } = new LabelOptions();

    /// <summary>
    /// Gets the execution configuration for the step.
    /// </summary>
    public ExecutionOptions ExecutionOptions { get; init; } = new ExecutionOptions();

    /// <summary>
    /// Gets the declared input and output contract for the step.
    /// </summary>
    public StepIOContract IOContract { get; init; } = new StepIOContract();

    /// <summary>
    /// Gets a value indicating whether the step returns a result value.
    /// </summary>
    public abstract bool DoesReturn { get; }

    /// <summary>
    /// Executes the step through the untyped base contract.
    /// </summary>
    public abstract Task<object?> ExecuteGeneric(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken);

    /// <summary>
    /// Declares the step input and output contract.
    /// </summary>
    /// <param name="contract">The contract object to populate.</param>
    public abstract void DeclareIO(StepIOContract contract);

    /// <summary>
    /// Creates an untyped runtime instance for this step.
    /// </summary>
    public abstract StepInstanceGeneric GetInstanceGeneric();

    /// <summary>
    /// Creates an untyped clone of this step definition.
    /// </summary>
    public abstract StepGeneric CloneGeneric();

    /// <summary>
    /// Copies the option state from another step into this instance.
    /// </summary>
    /// <param name="from">The step whose options should be copied.</param>
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