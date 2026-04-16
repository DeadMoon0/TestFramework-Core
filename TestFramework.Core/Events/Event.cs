using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Events;

#pragma warning disable CA1716 // Type names should not match keywords
public abstract class Event<TEvent, TResult> : Step<TResult> where TEvent : Event<TEvent, TResult>
#pragma warning restore CA1716
{
    public abstract Task<TResult?> DoEventPolling(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken);

    public override Task<TResult?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken) => DoEventPolling(serviceProvider, variableStore, artifactStore, logger, cancellationToken);

    public override StepInstance<Step<TResult>, TResult> GetInstance() => new StepInstance<Step<TResult>, TResult>(this);
}