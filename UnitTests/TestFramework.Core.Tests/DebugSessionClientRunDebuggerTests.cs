using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using TestFramework.Core.Debugger;

namespace TestFramework.Core.Tests;

public class DebugSessionClientRunDebuggerTests
{
    [Fact]
    public async Task PipeRunDebugger_ImplementsRunDebuggerContract()
    {
        PipeRunDebugger debugger = new();

        await debugger.SignalInitTimelineRunAsync("session-1", "Run", "project.csproj", new TimelineRunStructure
        {
            Variables = new Dictionary<TestFramework.Core.Variables.VariableIdentifier, VariableState>(),
            Artifacts = new Dictionary<TestFramework.Core.Artifacts.ArtifactIdentifier, ArtifactState>(),
            Stages = []
        });
        await debugger.SignalEntityTransitionAsync("session-1", DebugEntityKind.Run, null, null, DebugLifecycleState.Running);
        await debugger.SignalTimelineRunFinishedAsync("session-1");
        await debugger.SignalAndWaitBreakpointHitAsync("session-1", "Main", 2);
    }

    [Fact]
    public async Task CommonDebugger_UsesRegisteredRunDebugger()
    {
        RecordingRunDebugger registeredDebugger = new();
        TestServiceProvider serviceProvider = new(new Dictionary<Type, object>
        {
            [typeof(IRunDebugger)] = registeredDebugger
        });

        IRunDebugger debugger = CommonDebugger.GetCommon(serviceProvider, null);

        await debugger.SignalTimelineRunFinishedAsync("session-2");

        Assert.Equal("session-2", registeredDebugger.FinishedSessionId);
    }

    [Fact]
    public void CommonDebugger_ProvidesBuiltInPipeDebuggerWithoutReflection()
    {
        // The negative connect cache is process-wide, so without clearing it this test passes or
        // fails depending on whether another test already probed the pipe.
        PipeClient.ResetAvailabilityForTests();

        IRunDebugger debugger = CommonDebugger.GetCommon();

        PipeRunDebugger pipeDebugger = Assert.IsType<PipeRunDebugger>(debugger);
        Assert.NotNull(pipeDebugger);
    }

    [Fact]
    public async Task PipeClient_RemembersAMissedPipeForTheWholeProcess()
    {
        // This is the whole point of the negative cache: a fresh client is built for every run, so
        // an instance-level flag meant every run in the suite paid the same connect probe again.
        string pipeName = $"testframework-missing-{Guid.NewGuid():N}";
        Assert.False(PipeClient.IsKnownUnavailable(pipeName));

        using (PipeClient probe = new(pipeName))
        {
            await probe.SignalAsync(new PipeTimelineRunFinishedSignal { SessionId = "probe" });
        }

        Assert.True(PipeClient.IsKnownUnavailable(pipeName));

        // A different client for the same name inherits the knowledge rather than re-probing.
        using PipeClient later = new(pipeName);
        Stopwatch stopwatch = Stopwatch.StartNew();
        await later.SignalAsync(new PipeTimelineRunFinishedSignal { SessionId = "probe-2" });
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(100), $"A second client re-probed the pipe and took {stopwatch.Elapsed}.");
    }

    [Fact]
    public void PipeDebuggerEnabled_False_KeepsThePipeDebuggerOutOfTheFanOut()
    {
        PipeClient.ResetAvailabilityForTests();
        PipeDebuggerMode? previous = PipeTransport.ModeOverride;
        try
        {
            TestFrameworkDebugging.PipeDebuggerEnabled = false;

            Assert.False(TestFrameworkDebugging.PipeDebuggerEnabled);
            Assert.IsType<EmptyRunDebugger>(CommonDebugger.GetCommon());
        }
        finally
        {
            PipeTransport.ModeOverride = previous;
            PipeClient.ResetAvailabilityForTests();
        }
    }

    [Fact]
    public async Task PipeClient_ProbesBrieflyInAutoMode()
    {
        PipeClient.ResetAvailabilityForTests();
        using PipeClient client = new($"testframework-missing-{Guid.NewGuid():N}");

        Stopwatch stopwatch = Stopwatch.StartNew();
        await client.SignalAsync(new PipeTimelineRunFinishedSignal { SessionId = "session-1" });
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1), $"Auto-mode probe took {stopwatch.Elapsed}; it should give up in roughly 250 ms.");
    }

    [Fact]
    public async Task DebuggingRunSession_UsesCurrentXunitMethodName_ForRunName()
    {
        RecordingRunDebugger debugger = new();
        DebuggingRunSession session = new(debugger);

        await session.InitSessionAsync(new TimelineRunStructure
        {
            Variables = new Dictionary<TestFramework.Core.Variables.VariableIdentifier, VariableState>(),
            Artifacts = new Dictionary<TestFramework.Core.Artifacts.ArtifactIdentifier, ArtifactState>(),
            Stages = []
        });

        Assert.Equal(nameof(DebuggingRunSession_UsesCurrentXunitMethodName_ForRunName), debugger.InitializedRunName);
    }

    [Fact]
    public async Task DebuggingRunSession_UsesCurrentTestHostProcessPath_ForProjectPath()
    {
        RecordingRunDebugger debugger = new();
        DebuggingRunSession session = new(debugger);

        await session.InitSessionAsync(new TimelineRunStructure
        {
            Variables = new Dictionary<TestFramework.Core.Variables.VariableIdentifier, VariableState>(),
            Artifacts = new Dictionary<TestFramework.Core.Artifacts.ArtifactIdentifier, ArtifactState>(),
            Stages = []
        });

        Assert.Equal(System.Environment.ProcessPath, debugger.InitializedProjectPath);
        Assert.EndsWith("testhost.exe", debugger.InitializedProjectPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsCapturing_IsFalseOnlyWhenNothingDownstreamWantsTheSignals()
    {
        Assert.False(new EmptyRunDebugger().IsCapturing);
        Assert.True(((IRunDebugger)new RecordingRunDebugger()).IsCapturing);

        Assert.False(CompositeRunDebugger.Create([new EmptyRunDebugger(), new EmptyRunDebugger()]).IsCapturing);
        Assert.True(CompositeRunDebugger.Create([new EmptyRunDebugger(), new RecordingRunDebugger()]).IsCapturing);

        Assert.False(new DebuggingRunSession(new EmptyRunDebugger()).IsCapturing);
        Assert.True(new DebuggingRunSession(new RecordingRunDebugger()).IsCapturing);
    }

    [Fact]
    public void PipeRunDebugger_StopsCapturing_OnceThePipeIsKnownUnavailable()
    {
        PipeDebuggerMode? previous = PipeTransport.ModeOverride;
        try
        {
            TestFrameworkDebugging.PipeDebuggerEnabled = false;
            using PipeRunDebugger disabled = new();
            Assert.False(disabled.IsCapturing);
        }
        finally
        {
            PipeTransport.ModeOverride = previous;
        }
    }

    [Fact]
    public async Task PipeClient_DoesNotRepeatConnectTimeoutAfterInitialFailure()
    {
        PipeClient.ResetAvailabilityForTests();
        using PipeClient client = new($"testframework-missing-{Guid.NewGuid():N}");

        await client.SignalAsync(new PipeTimelineRunFinishedSignal { SessionId = "session-1" });

        Stopwatch stopwatch = Stopwatch.StartNew();
        await client.SignalAsync(new PipeTimelineRunFinishedSignal { SessionId = "session-2" });
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(250), $"Expected cached connect failure to return quickly, but it took {stopwatch.Elapsed}.");
    }

    private sealed class RecordingRunDebugger : IRunDebugger
    {
        public string FinishedSessionId { get; private set; } = string.Empty;
        public string InitializedRunName { get; private set; } = string.Empty;
        public string InitializedProjectPath { get; private set; } = string.Empty;

        public Task SignalInitTimelineRunAsync(string sessionId, string name, string projectPath, TimelineRunStructure runStructure)
        {
            InitializedRunName = name;
            InitializedProjectPath = projectPath;
            return Task.CompletedTask;
        }

        public Task SignalEntityTransitionAsync(string sessionId, DebugEntityKind entityKind, string? stage, int? stepId, DebugLifecycleState state, DebugLifecycleState? previousState = null, DebugLifecycleState? outcomeState = null)
            => Task.CompletedTask;

        public Task SignalValueUpdateAsync(string sessionId, string name, DebugValueKind valueKind, string? stage, int? stepId, DebugValueEnvelope value)
            => Task.CompletedTask;

        public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry)
            => Task.CompletedTask;

        public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry)
            => Task.CompletedTask;

        public Task SignalTimelineRunFinishedAsync(string sessionId)
        {
            FinishedSessionId = sessionId;
            return Task.CompletedTask;
        }

        public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId)
            => Task.CompletedTask;
    }

    private sealed class TestServiceProvider(Dictionary<Type, object> services) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return services.TryGetValue(serviceType, out object? service) ? service : null;
        }
    }
}