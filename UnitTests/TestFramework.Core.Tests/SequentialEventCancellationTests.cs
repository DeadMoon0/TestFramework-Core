using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Events;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

/// <summary>
/// A polling event that ignores the step timeout keeps the whole suite waiting for its next tick.
/// </summary>
public class SequentialEventCancellationTests
{
    [Fact]
    public async Task Polling_StopsAtTheStepTimeout_RatherThanAtTheNextPollingDelay()
    {
        CountingSequentialEvent step = new(TimeSpan.FromSeconds(10));
        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(1));

        Stopwatch stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => step.Execute(RunContext.Ambient(
            new EmptyServiceProvider(),
            CreateStore(),
            CreateArtifactStore(),
            new ScopedLogger(null),
            ValueResolution.Empty,
            cancellation.Token)));

        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Polling ran for {stopwatch.Elapsed}, so it waited out the 10s delay.");

        int pollsAtCancellation = step.PollCount;
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        Assert.Equal(pollsAtCancellation, step.PollCount);
    }

    private static VariableStore CreateStore() => new(new ScopedLogger(null), new DebuggingRunSession(new EmptyRunDebugger()));

    private static ArtifactStore CreateArtifactStore() => new(new ScopedLogger(null), new DebuggingRunSession(new EmptyRunDebugger()));

    private sealed class CountingSequentialEvent(TimeSpan nextDelay)
        : SequentialEvent<CountingSequentialEvent, EmptyStepResultContext>
    {
        private int _pollCount;

        public int PollCount => Volatile.Read(ref _pollCount);

        public override string Name => "counting-poll";
        public override string Description => "never completes";
        public override bool DoesReturn => false;

        public override Step<EmptyStepResultContext> Clone() => new CountingSequentialEvent(nextDelay).WithClonedOptions(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override Task<SequentialPollingResult<EmptyStepResultContext>> OnSequentialPolling(RunContext context)
        {
            Interlocked.Increment(ref _pollCount);
            return Task.FromResult(new SequentialPollingResult<EmptyStepResultContext>(false, null, nextDelay));
        }
    }
}
