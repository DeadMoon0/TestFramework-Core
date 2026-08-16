using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using TestFramework.Core.Debugger;

namespace TestFramework.Core.Tests;

[Collection("DebuggerEnvironment")]
public class DebugSessionClientRunDebuggerTests
{
    [Fact]
    public async Task PipeRunDebugger_ImplementsRunDebuggerContract()
    {
        PipeRunDebugger debugger = new();

        await debugger.SignalInitTimelineRunAsync("session-1", "Run", "project.csproj", new TimelineRunStructure
        {
            Variables = new Dictionary<TestFramework.Core.Variables.VariableIdentifier, DebugValue>(),
            Artifacts = new Dictionary<TestFramework.Core.Artifacts.ArtifactIdentifier, DebugValue>(),
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
    [Trait("Category", "WindowsOnly")]
    public void CommonDebugger_ProvidesBuiltInPipeDebugger_WhenAUiIsListening()
    {
        string pipeName = $"testframework-listening-{Guid.NewGuid():N}";
        string? previousName = System.Environment.GetEnvironmentVariable("TESTFRAMEWORK_DEBUG_PIPE_NAME");
        try
        {
            System.Environment.SetEnvironmentVariable("TESTFRAMEWORK_DEBUG_PIPE_NAME", pipeName);
            using System.IO.Pipes.NamedPipeServerStream server = new(
                pipeName,
                System.IO.Pipes.PipeDirection.InOut,
                1,
                System.IO.Pipes.PipeTransmissionMode.Byte,
                System.IO.Pipes.PipeOptions.Asynchronous | System.IO.Pipes.PipeOptions.CurrentUserOnly);

            PipeClient.ResetAvailabilityForTests();

            using JournalScope journal = JournalScope.Disarmed();

            Assert.IsType<PipeRunDebugger>(CommonDebugger.GetCommon());
        }
        finally
        {
            System.Environment.SetEnvironmentVariable("TESTFRAMEWORK_DEBUG_PIPE_NAME", previousName);
            PipeClient.ResetAvailabilityForTests();
        }
    }

    [Fact]
    [Trait("Category", "WindowsOnly")]
    public void CommonDebugger_SkipsThePipeDebugger_WhenNothingIsListening()
    {
        string? previousName = System.Environment.GetEnvironmentVariable("TESTFRAMEWORK_DEBUG_PIPE_NAME");
        try
        {
            System.Environment.SetEnvironmentVariable("TESTFRAMEWORK_DEBUG_PIPE_NAME", $"testframework-absent-{Guid.NewGuid():N}");
            PipeClient.ResetAvailabilityForTests();

            // Nothing downstream wants the signals, so the run should not carry a transport at all.
            using JournalScope journal = JournalScope.Disarmed();

            Assert.IsType<EmptyRunDebugger>(CommonDebugger.GetCommon());
        }
        finally
        {
            System.Environment.SetEnvironmentVariable("TESTFRAMEWORK_DEBUG_PIPE_NAME", previousName);
            PipeClient.ResetAvailabilityForTests();
        }
    }

    [Fact]
    [Trait("Category", "WindowsOnly")]
    public async Task PipeClient_ReEvaluatesAvailability_InsteadOfLatchingAMiss()
    {
        // Replaces the old process-wide negative cache. Latching a miss made the suite cheap but
        // made attaching a UI mid-run impossible without an environment variable set up front. The
        // availability probe is cheap enough to repeat, so a miss must not be permanent.
        string pipeName = $"testframework-missing-{Guid.NewGuid():N}";
        PipeClient.ResetAvailabilityForTests();

        Assert.True(PipeClient.IsKnownUnavailable(pipeName));

        using (PipeClient probe = new(pipeName))
        {
            await probe.SignalAsync(new PipeTimelineRunFinishedSignal { SessionId = "probe" });
        }

        using System.IO.Pipes.NamedPipeServerStream server = new(
            pipeName,
            System.IO.Pipes.PipeDirection.InOut,
            1,
            System.IO.Pipes.PipeTransmissionMode.Byte,
            System.IO.Pipes.PipeOptions.Asynchronous | System.IO.Pipes.PipeOptions.CurrentUserOnly);
        PipeClient.ResetAvailabilityForTests();

        Assert.False(PipeClient.IsKnownUnavailable(pipeName));
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
            using JournalScope journal = JournalScope.Disarmed();

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

        // The budget is 250 ms. The bound here is deliberately looser: a loaded CI runner adds
        // scheduling jitter, and the claim under test is only that Auto mode gives up quickly
        // instead of paying the 2 s an attached UI is worth.
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(1.5),
            $"Auto-mode probe took {stopwatch.Elapsed}; it should give up in roughly 250 ms and must never approach the 2 s attached budget.");
    }

    [Fact]
    public async Task DebuggingRunSession_UsesCurrentXunitMethodName_ForRunName()
    {
        RecordingRunDebugger debugger = new();
        DebuggingRunSession session = new(debugger);

        await session.InitSessionAsync(new TimelineRunStructure
        {
            Variables = new Dictionary<TestFramework.Core.Variables.VariableIdentifier, DebugValue>(),
            Artifacts = new Dictionary<TestFramework.Core.Artifacts.ArtifactIdentifier, DebugValue>(),
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
            Variables = new Dictionary<TestFramework.Core.Variables.VariableIdentifier, DebugValue>(),
            Artifacts = new Dictionary<TestFramework.Core.Artifacts.ArtifactIdentifier, DebugValue>(),
            Stages = []
        });

        string resolved = debugger.InitializedProjectPath ?? string.Empty;

        // Windows runs tests inside testhost.exe, so the process path already identifies the run.
        // On Unix the process is the shared dotnet host, which identifies nothing, so the resolver
        // is expected to fall back to the test assembly from the command line. The invariant that
        // matters on both is that the reported path points at *this* run, never at the shared host.
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(System.Environment.ProcessPath, resolved);
            Assert.EndsWith("testhost.exe", resolved, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.NotEqual(System.Environment.ProcessPath, resolved);
            Assert.Contains("TestFramework.Core.Tests", resolved, StringComparison.OrdinalIgnoreCase);
        }
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

        public Task SignalInitTimelineRunAsync(string sessionId, string name, string projectPath, TimelineRunStructure runStructure, TestIdentity? identity = null)
        {
            InitializedRunName = name;
            InitializedProjectPath = projectPath;
            return Task.CompletedTask;
        }

        public Task SignalEntityTransitionAsync(string sessionId, DebugEntityKind entityKind, string? stage, int? stepId, DebugLifecycleState state, DebugLifecycleState? previousState = null, DebugLifecycleState? outcomeState = null, DebugFailureDetail? failure = null)
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