using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

/// <summary>
/// Pins that a run always reaches its Cleanup stage, always freezes and always comes back, whatever
/// fails on the way.
/// </summary>
/// <remarks>
/// Each case names the damage. Before these held, an exception that escaped the step loop left the
/// stage loop too: the Cleanup stage never ran, so every artifact and environment component the run
/// had set up was stranded, and the run was never frozen or returned.
/// </remarks>
public sealed class RunTeardownGuaranteeTests
{
    [Fact]
    public async Task ADebuggerThatDiesWhileHoldingAStepDoesNotStrandTheRunsArtifacts()
    {
        // The live case: the UI closes while a step waits at a breakpoint, and the pipe's wait ends
        // in a cancellation instead of an answer.
        TeardownRecorder recorder = new();
        FailingDebugger debugger = new(failAtBreakpoint: true, failEverySignal: false);

        TimelineRun run = await OwnedArtifactTimeline(recorder)
            .SetupRun(new DebuggerServiceProvider(debugger))
            .RunAsync();

        Assert.Contains("work", recorder.Executed);
        Assert.Contains("owned", recorder.Deconstructed);
        Assert.True(run.IsFrozen, "A run that came back must be frozen.");
        Assert.Equal(StepState.Complete, run.Step("work").State);
    }

    [Fact]
    public async Task ADebuggerThatFailsEverySignalDoesNotStrandTheRunsArtifacts()
    {
        // A debugger watches the run; it has no say in how it goes. One that throws on every signal
        // must not fail the steps it was only told about - least of all the cleanup steps.
        TeardownRecorder recorder = new();
        FailingDebugger debugger = new(failAtBreakpoint: true, failEverySignal: true);

        TimelineRun run = await OwnedArtifactTimeline(recorder)
            .SetupRun(new DebuggerServiceProvider(debugger))
            .RunAsync();

        Assert.Contains("work", recorder.Executed);
        Assert.Contains("owned", recorder.Deconstructed);
        Assert.True(run.IsFrozen, "A run that came back must be frozen.");
        Assert.Equal(StepState.Complete, run.Step("work").State);
    }

    [Fact]
    public async Task ARetryCountNamingAVariableNothingSetsIsRefusedBeforeAnythingRuns()
    {
        // Read after the first failed attempt, the count used to throw out of the step loop mid-run.
        // It is an input of the step like any other, so the plan knows it is missing.
        TeardownRecorder recorder = new();

        Timeline timeline = Timeline.Create()
            .RegisterArtifact("owned", new RecordingReference(recorder))
            .SetupArtifact("owned")
            .Trigger(new FailingStep())
            .Label("flaky")
            .WithRetry(Var.Ref<int>("retries"), _ => TimeSpan.Zero)
            .Build();

        IOContractViolationException refusal = await Assert.ThrowsAsync<IOContractViolationException>(
            () => timeline.SetupRun().RunAsync());

        Assert.Contains("retries", refusal.FriendlyMessage);
        Assert.Empty(recorder.Executed);
    }

    [Fact]
    public async Task ARetryCountSetEarlierInTheRunIsAccepted()
    {
        // The other half of the refusal above: declaring the count as an input must not refuse the
        // ordinary case of a variable the run does set.
        Timeline timeline = Timeline.Create()
            .SetVariable("retries", Var.Const(1))
            .Trigger(new FailingStep())
            .Label("flaky")
            .WithRetry(Var.Ref<int>("retries"), _ => TimeSpan.Zero)
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        Assert.Equal(StepState.Error, run.Step("flaky").State);
        Assert.True(run.IsFrozen);
    }

    private static Timeline OwnedArtifactTimeline(TeardownRecorder recorder)
        => Timeline.Create()
            .RegisterArtifact("owned", new RecordingReference(recorder))
            .SetupArtifact("owned")
            .Trigger(new RecordingStep(recorder, "work"))
            .Label("work")
            .Build();

    private sealed class DebuggerServiceProvider(IRunDebugger debugger) : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => serviceType == typeof(IRunDebugger) ? debugger : null;
    }

    private sealed class FailingDebugger(bool failAtBreakpoint, bool failEverySignal) : IRunDebugger
    {
        public bool IsCapturing => true;

        private Task Signal() => failEverySignal
            ? Task.FromException(new InvalidOperationException("The debugger's consumer is gone."))
            : Task.CompletedTask;

        public Task SignalInitTimelineRunAsync(string sessionId, DebugRunStart start) => Signal();
        public Task SignalEntityTransitionAsync(string sessionId, DebugEntityTransition transition) => Signal();
        public Task SignalValueUpdateAsync(string sessionId, DebugValueUpdate update) => Signal();
        public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry) => Signal();
        public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry) => Signal();
        public Task SignalTimelineRunFinishedAsync(string sessionId) => Signal();

        public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId) => failAtBreakpoint
            ? Task.FromException(new TaskCanceledException("The pipe closed while the step was held."))
            : Task.CompletedTask;
    }

    private sealed class TeardownRecorder
    {
        public List<string> Executed { get; } = [];
        public List<string> Deconstructed { get; } = [];
    }

    private sealed class RecordingStep(TeardownRecorder recorder, string name) : Step<EmptyStepResultContext>
    {
        public override string Name => name;
        public override string Description => "Records that it ran.";
        public override bool DoesReturn => false;

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
        {
            recorder.Executed.Add(name);
            return Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
        }

        public override Step<EmptyStepResultContext> Clone() => new RecordingStep(recorder, name).WithClonedOptions(this);
        public override void DeclareIO(StepIOContract contract) { }
        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }

    private sealed class FailingStep : Step<EmptyStepResultContext>
    {
        public override string Name => "failing";
        public override string Description => "Always fails.";
        public override bool DoesReturn => false;

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
            => throw new InvalidOperationException("This step always fails.");

        public override Step<EmptyStepResultContext> Clone() => new FailingStep().WithClonedOptions(this);
        public override void DeclareIO(StepIOContract contract) { }
        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }

    private sealed class RecordingData : ArtifactData<RecordingData, RecordingDescriber, RecordingReference>
    {
        public override string ToString() => "recording-data";
    }

    private sealed class RecordingDescriber : ArtifactDescriber<RecordingDescriber, RecordingData, RecordingReference>
    {
        public override Task Setup(RunContext context, RecordingData data, RecordingReference reference)
            => Task.CompletedTask;

        public override Task Deconstruct(RunContext context, RecordingReference reference)
        {
            reference.RecordDeconstructed();
            return Task.CompletedTask;
        }

        public override string ToString() => "recording-artifact";
    }

    private sealed class RecordingReference : ArtifactReference<RecordingReference, RecordingDescriber, RecordingData>
    {
        private readonly TeardownRecorder recorder;

        public RecordingReference(TeardownRecorder recorder)
        {
            this.recorder = recorder;
            CanDeconstruct = true;
        }

        public void RecordDeconstructed() => recorder.Deconstructed.Add("owned");

        public override Task<ArtifactResolveResult<RecordingDescriber, RecordingData, RecordingReference>> ResolveToDataAsync(RunContext context, ArtifactVersionIdentifier versionIdentifier)
            => Task.FromResult(new ArtifactResolveResult<RecordingDescriber, RecordingData, RecordingReference>
            {
                Found = true,
                Data = new RecordingData()
            });

        public override void DeclareIO(StepIOContract contract) { }
        public override void OnPinReference(RunContext context) { }
        public override string ToString() => "recording-reference";
    }
}
