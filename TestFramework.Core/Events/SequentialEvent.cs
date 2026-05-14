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
    /// <param name="serviceProvider">The service provider available to the event.</param>
    /// <param name="variableStore">The current run variable store.</param>
    /// <param name="artifactStore">The current run artifact store.</param>
    /// <param name="logger">The scoped logger for the run.</param>
    /// <param name="cancellationToken">The cancellation token for the running step.</param>
    public abstract Task<SequentialPollingResult<TStepResultContext>> OnSequentialPolling(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken);

    /// <summary>
    /// Executes the polling loop until a completed result is returned.
    /// </summary>
    public override async Task<TStepResultContext?> DoEventPolling(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
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