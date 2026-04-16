using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Events;

public record SequentialPollingResult<TResult>(bool IsDone, TResult? Result, TimeSpan NextDelay);

public abstract class SequentialEvent<TEvent, TResult> : Event<TEvent, TResult> where TEvent : SequentialEvent<TEvent, TResult>
{
    public abstract Task<SequentialPollingResult<TResult>> OnSequentialPolling(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken);

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