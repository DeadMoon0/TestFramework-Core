using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;
using Xunit;
using Xunit.Abstractions;

namespace TestFramework.Core.Tests;

/// <summary>
/// What a reader is told when a step runs out of time, and what an abandoned attempt is still allowed to
/// do afterwards.
/// </summary>
public class StepTimeoutGraceTests(ITestOutputHelper output)
{
    [Fact]
    public async Task AStepThatStopsCooperativelyGetsItsOwnAccountSurfaced()
    {
        // The reason two packages hand-rolled their own margins: without the grace window this exception
        // was raised into a task nobody awaited any more, and the reader got "it timed out" instead of
        // what the step was actually waiting for.
        Timeline timeline = Timeline.Create()
            .Trigger(new CooperativeStep("waiting for the warehouse to answer"))
                .WithTimeOut(TimeSpan.FromMilliseconds(300)).Name("cooperative")
            .Build();

        TimelineRun run = await timeline.SetupRun(outputHelper: output).RunAsync();

        StepResultGeneric result = run.Step("cooperative").LastResult;

        Assert.Equal(StepState.Timeout, result.State);
        Assert.Contains("waiting for the warehouse to answer", result.Exception!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AStepThatIgnoresItsDeadlineIsAbandonedWithTheGenericMessage()
    {
        // Nothing to surface: a step that will not stop has said nothing, so the honest message is that
        // it timed out.
        using ManualResetEventSlim release = new ManualResetEventSlim(false);

        Timeline timeline = Timeline.Create()
            .Trigger(new StubbornStep(release))
                .WithTimeOut(TimeSpan.FromMilliseconds(300)).Name("stubborn")
            .Build();

        TimelineRun run = await timeline.SetupRun(outputHelper: output).RunAsync();

        StepResultGeneric result = run.Step("stubborn").LastResult;

        Assert.Equal(StepState.Timeout, result.State);
        Assert.IsType<TimeoutException>(result.Exception);
        Assert.Contains("timed out after", result.Exception!.Message, StringComparison.Ordinal);

        release.Set();
    }

    [Fact]
    public void AnAbandonedAttemptsWritesAreRefusedRatherThanLanding()
    {
        // The suspected mechanism behind a suite that fails differently under load: a step abandoned in
        // one test still writing while the next one reads.
        VariableStore store = new VariableStore(new ScopedLogger(null), new DebuggingRunSession(new EmptyRunDebugger()));
        StepAttemptGate gate = new StepAttemptGate();

        StepAttempt abandoned = gate.Begin("slow-step", 1);
        VariableStore abandonedView = store.ForAttempt(gate, abandoned);

        abandonedView.SetVariable("beforeAbandonment", "landed");

        // The run moves on - a retry, or simply the next step.
        StepAttempt live = gate.Begin("next-step", 1);
        VariableStore liveView = store.ForAttempt(gate, live);

        abandonedView.SetVariable("afterAbandonment", "should not land");
        liveView.SetVariable("fromTheLiveAttempt", "landed");

        Assert.True(store.TryGetVariable<string>("beforeAbandonment", out _));
        Assert.False(store.TryGetVariable<string>("afterAbandonment", out _));
        Assert.True(store.TryGetVariable<string>("fromTheLiveAttempt", out _));
    }

    [Fact]
    public void WritesThatBelongToNoAttemptStillLand()
    {
        // A fixture seeding a variable, or the run publishing its own summary.
        VariableStore store = new VariableStore(new ScopedLogger(null), new DebuggingRunSession(new EmptyRunDebugger()));
        StepAttemptGate gate = new StepAttemptGate();

        gate.Begin("some-step", 1);
        store.SetVariable("seeded", "landed");

        Assert.True(store.TryGetVariable<string>("seeded", out _));
    }

    /// <summary>A step that notices its deadline and says what it was doing.</summary>
    private sealed class CooperativeStep(string waitingFor) : Step<EmptyStepResultContext>
    {
        public override string Name => "Cooperative";

        public override string Description => "Stops when told, and says what it was waiting for.";

        public override bool DoesReturn => false;

        public override Step<EmptyStepResultContext> Clone() => new CooperativeStep(waitingFor).WithClonedOptions(this);

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override async Task<EmptyStepResultContext?> Execute(
            IServiceProvider serviceProvider,
            VariableStore variableStore,
            ArtifactStore artifactStore,
            ScopedLogger logger,
            CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // What the step knows and the runner does not.
                throw new TimeoutException($"Gave up {waitingFor}.");
            }

            return EmptyStepResultContext.Instance;
        }
    }

    /// <summary>
    /// A step that ignores cancellation. It yields - a step that blocks its thread blocks the runner
    /// itself, which is a different fault and not the one under test here.
    /// </summary>
    private sealed class StubbornStep(ManualResetEventSlim release) : Step<EmptyStepResultContext>
    {
        public override string Name => "Stubborn";

        public override string Description => "Ignores cancellation entirely.";

        public override bool DoesReturn => false;

        public override Step<EmptyStepResultContext> Clone() => new StubbornStep(release).WithClonedOptions(this);

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override async Task<EmptyStepResultContext?> Execute(
            IServiceProvider serviceProvider,
            VariableStore variableStore,
            ArtifactStore artifactStore,
            ScopedLogger logger,
            CancellationToken cancellationToken)
        {
            // No token anywhere: the step never learns it should stop.
            while (!release.IsSet)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), CancellationToken.None);
            }

            return EmptyStepResultContext.Instance;
        }
    }
}
