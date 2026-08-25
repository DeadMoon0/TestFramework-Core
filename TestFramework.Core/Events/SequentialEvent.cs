using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Events;

/// <summary>
/// Represents the outcome of a single polling iteration for a <see cref="SequentialEvent{TEvent, TResult}"/>.
/// </summary>
/// <typeparam name="TStepResultContext">The result context type produced by the event.</typeparam>
/// <param name="IsDone">Indicates whether polling is complete.</param>
/// <param name="Result">The result value when polling is complete.</param>
/// <param name="NextDelay">The delay before the next polling iteration when polling is not complete.</param>
public record SequentialPollingResult<TStepResultContext>(bool IsDone, TStepResultContext? Result, TimeSpan NextDelay) where TStepResultContext : StepResultContext;

/// <summary>
/// Represents an event step that polls repeatedly until a terminating condition is reached.
/// </summary>
/// <typeparam name="TEvent">The concrete event type.</typeparam>
/// <typeparam name="TStepResultContext">The result context type produced by the event.</typeparam>
public abstract class SequentialEvent<TEvent, TStepResultContext> : Event<TEvent, TStepResultContext>
    where TEvent : SequentialEvent<TEvent, TStepResultContext>
    where TStepResultContext : StepResultContext
{
    /// <summary>
    /// Performs a single polling iteration.
    /// </summary>
    /// <param name="context">What this event is given.</param>
    /// <returns>Whether the wait is over, and the result when it is.</returns>
    public abstract Task<SequentialPollingResult<TStepResultContext>> OnSequentialPolling(RunContext context);

    /// <summary>
    /// Executes the polling loop until a completed result is returned.
    /// </summary>
    /// <remarks>
    /// The loop runs on the caller's thread. An <see cref="OnSequentialPolling"/> implementation that
    /// blocks synchronously therefore blocks the start of its execution layer; make it genuinely async.
    /// In exchange, cancelling the step stops the loop instead of merely abandoning it.
    /// </remarks>
    /// <param name="context">What this event is given.</param>
    /// <returns>The event's result.</returns>
    public override async Task<TStepResultContext?> DoEventPolling(RunContext context)
    {
        // No Task.Run wrapper: WaitAsync would abandon the loop rather than stop it, so a long
        // NextDelay kept polling long after the step had been cancelled.
        while (true)
        {
            context.Deadline.Token.ThrowIfCancellationRequested();

            SequentialPollingResult<TStepResultContext> result = await OnSequentialPolling(context);

            if (result.IsDone) return result.Result;

            await Task.Delay(result.NextDelay, context.Deadline.Token);
        }
    }
}