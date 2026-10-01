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
/// <remarks>
/// Built with <see cref="Done"/> or <see cref="NotYet"/>, because the two halves never mix: a finished poll has
/// a result and no next delay, an unfinished one has a delay and no result. The positional constructor this
/// replaced took all three, so <c>(false, null, delay)</c> was how a poll said "not yet", and nothing stopped a
/// result being passed with <c>false</c> and silently dropped.
/// </remarks>
/// <typeparam name="TStepResultContext">The result context type produced by the event.</typeparam>
public sealed record SequentialPollingResult<TStepResultContext> where TStepResultContext : StepResultContext
{
    private SequentialPollingResult(bool isDone, TStepResultContext? result, TimeSpan nextDelay)
    {
        this.IsDone = isDone;
        this.Result = result;
        this.NextDelay = nextDelay;
    }

    /// <summary>
    /// Gets a value indicating whether polling is complete.
    /// </summary>
    public bool IsDone { get; }

    /// <summary>
    /// Gets the result, once polling is complete; <see langword="null"/> for an event that returns nothing.
    /// </summary>
    public TStepResultContext? Result { get; }

    /// <summary>
    /// Gets how long to wait before polling again, while polling is not complete.
    /// </summary>
    public TimeSpan NextDelay { get; }

    /// <summary>
    /// Polling is complete.
    /// </summary>
    /// <param name="result">What the event produced, or <see langword="null"/> for an event that returns nothing.</param>
    /// <returns>The finished poll.</returns>
    public static SequentialPollingResult<TStepResultContext> Done(TStepResultContext? result) => new(true, result, TimeSpan.Zero);

    /// <summary>
    /// The condition has not been reached yet; poll again after <paramref name="nextDelay"/>.
    /// </summary>
    /// <param name="nextDelay">How long to wait before polling again.</param>
    /// <returns>The unfinished poll.</returns>
    public static SequentialPollingResult<TStepResultContext> NotYet(TimeSpan nextDelay) => new(false, null, nextDelay);
}

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