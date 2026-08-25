using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Events;

#pragma warning disable CA1716 // Type names should not match keywords
/// <summary>
/// Represents a step that waits for an external event and yields a result once the event condition is met.
/// </summary>
/// <typeparam name="TEvent">The concrete event type.</typeparam>
/// <typeparam name="TStepResultContext">The result context type produced by the event.</typeparam>
public abstract class Event<TEvent, TStepResultContext> : Step<TStepResultContext> where TEvent : Event<TEvent, TStepResultContext> where TStepResultContext : StepResultContext
#pragma warning restore CA1716
{
    /// <inheritdoc />
    /// <inheritdoc />
    public override StepExecutionPhase Phase => StepExecutionPhase.Observe;

    /// <summary>
    /// Performs the event polling logic until a result is available.
    /// </summary>
    /// <remarks>
    /// An event knows how long it has through <c>context.Deadline</c>, so it can say what it was waiting
    /// for instead of being cut off mid-wait with nothing to report.
    /// </remarks>
    /// <param name="context">What this event is given.</param>
    /// <returns>The event's result.</returns>
    public abstract Task<TStepResultContext?> DoEventPolling(RunContext context);

    /// <summary>
    /// Executes the event by delegating to <see cref="DoEventPolling(RunContext)"/>.
    /// </summary>
    /// <param name="context">What this event is given.</param>
    /// <returns>The event's result.</returns>
    public override Task<TStepResultContext?> Execute(RunContext context) => DoEventPolling(context);

    /// <summary>
    /// Creates a runtime instance for the event step.
    /// </summary>
    public override StepInstance<Step<TStepResultContext>, TStepResultContext> GetInstance() => new StepInstance<Step<TStepResultContext>, TStepResultContext>(this);
}