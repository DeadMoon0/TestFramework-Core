using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
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
    /// <summary>
    /// Performs the event polling logic until a result is available.
    /// </summary>
    /// <param name="serviceProvider">The service provider available to the event.</param>
    /// <param name="variableStore">The current run variable store.</param>
    /// <param name="artifactStore">The current run artifact store.</param>
    /// <param name="logger">The scoped logger for the run.</param>
    /// <param name="cancellationToken">The cancellation token for the running step.</param>
    public abstract Task<TStepResultContext?> DoEventPolling(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken);

    /// <summary>
    /// Executes the event by delegating to <see cref="DoEventPolling(IServiceProvider, VariableStore, ArtifactStore, ScopedLogger, CancellationToken)"/>.
    /// </summary>
    public override Task<TStepResultContext?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken) => DoEventPolling(serviceProvider, variableStore, artifactStore, logger, cancellationToken);

    /// <summary>
    /// Creates a runtime instance for the event step.
    /// </summary>
    public override StepInstance<Step<TStepResultContext>, TStepResultContext> GetInstance() => new StepInstance<Step<TStepResultContext>, TStepResultContext>(this);
}