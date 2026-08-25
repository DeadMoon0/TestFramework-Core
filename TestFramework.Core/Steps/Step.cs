using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;
using System.ComponentModel;

namespace TestFramework.Core.Steps;

#pragma warning disable CA1716 // Type names should not match keywords
/// <summary>
/// Represents a typed executable step in a timeline.
/// </summary>
/// <typeparam name="TStepResultContext">The result context type produced by the step.</typeparam>
public abstract class Step<TStepResultContext> : StepGeneric where TStepResultContext : StepResultContext
#pragma warning restore CA1716
{
    /// <summary>
    /// Gets the CLR type produced by this step.
    /// </summary>
    public override Type ResultType => typeof(TStepResultContext);

    /// <summary>
    /// Executes the step.
    /// </summary>
    /// <remarks>
    /// Everything the step is given arrives on the context, including how long it has
    /// (<c>context.Deadline</c>) and where the run's resources ended up (<c>context.Values</c>). There is
    /// no separate cancellation token: a step whose token could differ from its own deadline is a step
    /// that believes it has time it does not have.
    /// </remarks>
    /// <param name="context">What this step is given.</param>
    /// <returns>The step's result.</returns>
    public abstract Task<TStepResultContext?> Execute(RunContext context);

    /// <summary>
    /// Creates a runtime instance for this step.
    /// </summary>
    public abstract StepInstance<Step<TStepResultContext>, TStepResultContext> GetInstance();

    /// <summary>
    /// Creates a clone of this step definition.
    /// </summary>
    public abstract Step<TStepResultContext> Clone();

    /// <summary>
    /// Copies the option state from another step clone into this instance.
    /// </summary>
    /// <typeparam name="TStep">The concrete step type.</typeparam>
    /// <param name="from">The step whose options should be copied.</param>
    public TStep WithClonedOptions<TStep>(TStep from) where TStep : Step<TStepResultContext> => (TStep)base.WithClonedOptions(from);

    /// <summary>
    /// Executes the step through the untyped base contract.
    /// </summary>
    /// <param name="context">What this step is given.</param>
    /// <returns>The step's result.</returns>
    public override async Task<object?> ExecuteGeneric(RunContext context) => await Execute(context);

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
    /// <remarks>
    /// The options are found rather than listed. A hand-written list is one line away from being wrong
    /// every time an option object is added - and it was: <c>LabelOptions</c> was missing from it, so a
    /// frozen step's label could still be changed while everything else about it was settled.
    /// </remarks>
    public void Freeze()
    {
        IsFrozen = true;

        foreach ((string _, IFreezable part) in this.FrameworkOptions())
        {
            part.Freeze();
        }
    }

    /// <summary>
    /// The framework's own option objects on this step, by property name.
    /// </summary>
    /// <remarks>
    /// Read from <see cref="StepGeneric"/> itself, never from the concrete step: a derived step may
    /// expose freezable things it shares with the rest of the run - an artifact reference, say - and
    /// freezing those here would settle them for everyone else too.
    /// </remarks>
    /// <returns>The option objects.</returns>
    internal IEnumerable<(string Name, IFreezable Part)> FrameworkOptions()
    {
        foreach (PropertyInfo property in optionProperties)
        {
            if (property.GetValue(this) is IFreezable part)
            {
                yield return (property.Name, part);
            }
        }
    }

    /// <summary>
    /// Every freezable option declared on <see cref="StepGeneric"/>. Computed once; the set is a fact
    /// about the type, not about any step.
    /// </summary>
    private static readonly PropertyInfo[] optionProperties =
        [.. typeof(StepGeneric)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(static property => typeof(IFreezable).IsAssignableFrom(property.PropertyType))
            .OrderBy(static property => property.Name, StringComparer.Ordinal)];

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
    /// Gets the label configuration for the step. Prefer the fluent <c>Name(...)</c> modifier when authoring timelines.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public LabelOptions LabelOptions { get; init; } = new LabelOptions();

    /// <summary>
    /// Gets the execution configuration for the step.
    /// </summary>
    public ExecutionOptions ExecutionOptions { get; init; } = new ExecutionOptions();

    /// <summary>
    /// Gets the result-target configuration for the step.
    /// </summary>
    public ResultOptions ResultOptions { get; init; } = new ResultOptions();

    /// <summary>
    /// Gets the workflow phase used by the stage planner when deciding which authored steps may share a layer.
    /// </summary>
    public virtual StepExecutionPhase Phase => StepExecutionPhase.Act;

    /// <summary>
    /// Gets the declared input and output contract for the step.
    /// </summary>
    public StepIOContract IOContract { get; init; } = new StepIOContract();

    /// <summary>
    /// Gets a value indicating whether the step returns a result value.
    /// </summary>
    public abstract bool DoesReturn { get; }

    /// <summary>
    /// Gets the CLR type produced by this step.
    /// </summary>
    public abstract Type ResultType { get; }

    /// <summary>
    /// Executes the step through the untyped base contract.
    /// </summary>
    /// <param name="context">What this step is given.</param>
    /// <returns>The step's result.</returns>
    public abstract Task<object?> ExecuteGeneric(RunContext context);

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
        from.ResultOptions.CloneTo(ResultOptions);
        from.IOContract.CloneTo(IOContract);
        return this;
    }

}