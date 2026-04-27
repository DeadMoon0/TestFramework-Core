using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Events;

/// <summary>
/// Represents the outcome of a single polling iteration for a <see cref="SequentialEvent{TEvent, TResult}"/>.
/// </summary>
/// <typeparam name="TResult">The result type produced by the event.</typeparam>
/// <param name="IsDone">Indicates whether polling is complete.</param>
/// <param name="Result">The result value when polling is complete.</param>
/// <param name="NextDelay">The delay before the next polling iteration when polling is not complete.</param>
public record SequentialPollingResult<TResult>(bool IsDone, TResult? Result, TimeSpan NextDelay);

/// <summary>
/// Represents an event step that polls repeatedly until a terminating condition is reached.
/// </summary>
/// <typeparam name="TEvent">The concrete event type.</typeparam>
/// <typeparam name="TResult">The result type produced by the event.</typeparam>
public abstract class SequentialEvent<TEvent, TResult> : Event<TEvent, TResult> where TEvent : SequentialEvent<TEvent, TResult>
{
    /// <summary>
    /// Performs a single polling iteration.
    /// </summary>
    /// <param name="serviceProvider">The service provider available to the event.</param>
    /// <param name="variableStore">The current run variable store.</param>
    /// <param name="artifactStore">The current run artifact store.</param>
    /// <param name="logger">The scoped logger for the run.</param>
    /// <param name="cancellationToken">The cancellation token for the running step.</param>
    public abstract Task<SequentialPollingResult<TResult>> OnSequentialPolling(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken);

    /// <summary>
    /// Executes the polling loop until a completed result is returned.
    /// </summary>
    public override async Task<TResult?> DoEventPolling(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        return await Task.Run(async () =>
        {
            do
            {
                var result = await OnSequentialPolling(serviceProvider, variableStore, artifactStore, logger, cancellationToken);
                if (result.IsDone) return result.Result;
                await Task.Delay(result.NextDelay);
            }
            while (true);
        }).WaitAsync(cancellationToken);
    }
}