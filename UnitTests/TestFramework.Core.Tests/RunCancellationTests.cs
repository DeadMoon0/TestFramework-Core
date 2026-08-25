using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Logging;
using TestFramework.Core.Stages;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

/// <summary>
/// Covers stopping a run cooperatively rather than killing its process.
/// </summary>
/// <remarks>
/// The distinction is the whole point. A timeline that unwinds runs its Cleanup stage, so artifacts
/// are deconstructed and environment components torn down; terminating the test host skips all of
/// that and strands containers, temp files and database rows. So cancellation must stop the work and
/// still reach teardown.
/// </remarks>
public sealed class RunCancellationTests
{
    [Fact]
    public async Task CancellingSkipsRemainingWorkButStillRunsCleanup()
    {
        CancellingDebugger debugger = new();
        TeardownRecorder recorder = new();

        Timeline timeline = Timeline.Create()
            .RegisterArtifact("owned", new RecordingReference(recorder))
            .SetupArtifact("owned")
            .Trigger(new SignallingStep(debugger))
            .Name("cancels")
            .Trigger(new RecordingStep(recorder, "after-cancel"))
            .Name("later")
            .Build();

        await timeline.SetupRun(new DebuggerServiceProvider(debugger)).RunAsync();

        // The step after the cancellation point must not have run...
        Assert.DoesNotContain("after-cancel", recorder.Executed);

        // ...but the artifact it created must still have been cleaned up.
        Assert.Contains("owned", recorder.Deconstructed);
    }

    [Fact]
    public async Task ARunningStepIsToldToStop()
    {
        // An in-flight step observes the token, so a long wait unwinds instead of running to
        // completion after the user already asked for the run to end.
        CancellingDebugger debugger = new();

        Timeline timeline = Timeline.Create()
            .Trigger(new CancelThenWaitStep(debugger))
            .Name("waits")
            .Build();

        await timeline.SetupRun(new DebuggerServiceProvider(debugger)).RunAsync();

        Assert.True(debugger.SawCancelledStep, "The running step should have observed the run's cancellation token.");
    }

    [Fact]
    public void CancellationIsIdempotentAndKeepsTheFirstReason()
    {
        DebuggingRunSession session = new(new EmptyRunDebugger());

        session.RequestCancellation("first");
        session.RequestCancellation("second");

        Assert.True(session.IsCancellationRequested);
        Assert.Equal("first", session.CancellationReason);
    }

    [Fact]
    public void ASessionWithNoRequestIsNotCancelled()
    {
        DebuggingRunSession session = new(new EmptyRunDebugger());

        Assert.False(session.IsCancellationRequested);
        Assert.False(session.RunCancellationToken.IsCancellationRequested);
    }

    [Fact]
    public void ADebuggerThatSupportsCancellationIsSubscribedAutomatically()
    {
        // The session opts in by capability rather than IRunDebugger carrying an inbound channel
        // that almost no implementation has.
        CancellableDebugger debugger = new();
        DebuggingRunSession session = new(debugger);

        debugger.Raise("stop please");

        Assert.True(session.IsCancellationRequested);
        Assert.Equal("stop please", session.CancellationReason);
    }

    private sealed class DebuggerServiceProvider(IRunDebugger debugger) : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => serviceType == typeof(IRunDebugger) ? debugger : null;
    }

    private sealed class CancellableDebugger : IRunDebugger, ISupportsRunCancellation
    {
        public event Action<string?>? CancellationRequested;

        public void Raise(string? reason) => CancellationRequested?.Invoke(reason);

        public bool IsCapturing => true;

        public Task SignalInitTimelineRunAsync(string sessionId, string name, string projectPath, TimelineRunStructure runStructure, TestIdentity? identity = null) => Task.CompletedTask;
        public Task SignalEntityTransitionAsync(string sessionId, DebugEntityKind entityKind, string? stage, int? stepId, DebugLifecycleState state, DebugLifecycleState? previousState = null, DebugLifecycleState? outcomeState = null, DebugFailureDetail? failure = null) => Task.CompletedTask;
        public Task SignalValueUpdateAsync(string sessionId, string name, DebugValueKind valueKind, string? stage, int? stepId, DebugValueEnvelope value) => Task.CompletedTask;
        public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry) => Task.CompletedTask;
        public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry) => Task.CompletedTask;
        public Task SignalTimelineRunFinishedAsync(string sessionId) => Task.CompletedTask;
        public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId) => Task.CompletedTask;
    }

    /// <summary>Lets a step ask the run to stop mid-flight, the way a UI would.</summary>
    private sealed class CancellingDebugger : IRunDebugger, ISupportsRunCancellation
    {
        public event Action<string?>? CancellationRequested;

        public bool SawCancelledStep { get; set; }

        public void RequestStop() => CancellationRequested?.Invoke("test asked to stop");

        public bool IsCapturing => true;

        public Task SignalInitTimelineRunAsync(string sessionId, string name, string projectPath, TimelineRunStructure runStructure, TestIdentity? identity = null) => Task.CompletedTask;
        public Task SignalEntityTransitionAsync(string sessionId, DebugEntityKind entityKind, string? stage, int? stepId, DebugLifecycleState state, DebugLifecycleState? previousState = null, DebugLifecycleState? outcomeState = null, DebugFailureDetail? failure = null) => Task.CompletedTask;
        public Task SignalValueUpdateAsync(string sessionId, string name, DebugValueKind valueKind, string? stage, int? stepId, DebugValueEnvelope value) => Task.CompletedTask;
        public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry) => Task.CompletedTask;
        public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry) => Task.CompletedTask;
        public Task SignalTimelineRunFinishedAsync(string sessionId) => Task.CompletedTask;
        public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId) => Task.CompletedTask;
    }

    private sealed class TeardownRecorder
    {
        public List<string> Executed { get; } = [];
        public List<string> Deconstructed { get; } = [];
    }

    private sealed class SignallingStep(CancellingDebugger debugger) : Step<EmptyStepResultContext>
    {
        public override string Name => "signalling";
        public override string Description => "Asks the run to stop.";
        public override bool DoesReturn => false;

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
        {
            debugger.RequestStop();
            return Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
        }

        public override Step<EmptyStepResultContext> Clone() => new SignallingStep(debugger).WithClonedOptions(this);
        public override void DeclareIO(StepIOContract contract) { }
        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }

    private sealed class CancelThenWaitStep(CancellingDebugger debugger) : Step<EmptyStepResultContext>
    {
        public override string Name => "cancel-then-wait";
        public override string Description => "Asks the run to stop, then waits on the token.";
        public override bool DoesReturn => false;

        public override async Task<EmptyStepResultContext?> Execute(RunContext context)
        {
            debugger.RequestStop();

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), context.Deadline.Token);
            }
            catch (OperationCanceledException)
            {
                debugger.SawCancelledStep = true;
                throw;
            }

            return EmptyStepResultContext.Instance;
        }

        public override Step<EmptyStepResultContext> Clone() => new CancelThenWaitStep(debugger).WithClonedOptions(this);
        public override void DeclareIO(StepIOContract contract) { }
        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
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

            // Owned rather than observed, or teardown passes over it by design and the test would
            // pass for the wrong reason.
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
