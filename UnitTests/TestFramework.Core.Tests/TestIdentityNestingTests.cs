using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Timelines;
using TestFramework.Core.Timelines.Builder.TimelineRunBuilder;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

/// <summary>
/// Covers identity resolution when the run is not started directly in the test method body.
/// </summary>
/// <remarks>
/// Real suites wrap runs in helpers, fixtures and local functions, and an async helper that has
/// already awaited no longer has the test method on its stack at all. If resolution happened late,
/// those runs would silently report <see cref="TestFrameworkKind.Unknown"/> — and the symptom is
/// only ever a missing name in the UI, so it would go unnoticed for a long time.
/// </remarks>
public sealed class TestIdentityNestingTests
{
    [Fact]
    public async Task ResolvesThroughASynchronousHelper()
    {
        IdentityRecordingDebugger debugger = new();

        await StartViaHelperAsync(debugger);

        AssertIdentifiedAs(debugger, nameof(ResolvesThroughASynchronousHelper));
    }

    [Fact]
    public async Task ResolvesThroughALocalFunction()
    {
        IdentityRecordingDebugger debugger = new();

        async Task StartAsync()
        {
            Timeline timeline = Timeline.Create().Build();
            await timeline.SetupRun(new DebuggerServiceProvider(debugger)).RunAsync();
        }

        await StartAsync();

        AssertIdentifiedAs(debugger, nameof(ResolvesThroughALocalFunction));
    }

    [Fact]
    public async Task StillLocatesTheCallSiteWhenAnAsyncHelperHasAlreadyAwaited()
    {
        // DOCUMENTS A REAL LIMIT. Once a helper awaits, its continuation runs on a state-machine
        // frame and the test method is genuinely not on the stack any more — no amount of walking
        // finds it. What survives is the compile-time call site, which is enough to show where the
        // run came from, and CanRerun correctly reports that a filter cannot be built.
        IdentityRecordingDebugger debugger = new();

        await StartAfterAwaitingAsync(debugger);

        TestIdentity identity = Assert.IsType<TestIdentity>(debugger.Identity);

        Assert.Equal(TestFrameworkKind.Unknown, identity.Framework);
        Assert.Null(identity.FullyQualifiedName);
        Assert.False(identity.CanRerun);

        // The location is still right, because the compiler recorded it at the call site.
        Assert.EndsWith(nameof(TestIdentityNestingTests) + ".cs", identity.SourceFilePath!, StringComparison.Ordinal);
        Assert.True(identity.SourceLineNumber > 0);
        Assert.EndsWith("TestFramework.Core.Tests.csproj", identity.ProjectFilePath!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StillLocatesTheCallSiteWhenStartedInsideATaskRun()
    {
        // Same limit, reached a different way: Task.Run hands the work to a thread-pool thread whose
        // stack contains none of this test.
        IdentityRecordingDebugger debugger = new();

        await Task.Run(async () =>
        {
            Timeline timeline = Timeline.Create().Build();
            await timeline.SetupRun(new DebuggerServiceProvider(debugger)).RunAsync();
        });

        TestIdentity identity = Assert.IsType<TestIdentity>(debugger.Identity);

        Assert.Equal(TestFrameworkKind.Unknown, identity.Framework);
        Assert.False(identity.CanRerun);
        Assert.EndsWith(nameof(TestIdentityNestingTests) + ".cs", identity.SourceFilePath!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolvesWhenTheBuilderIsHeldBeforeBeingRun()
    {
        // SetupRun and RunAsync need not be adjacent; a fixture may build the run and hand it on.
        IdentityRecordingDebugger debugger = new();

        Timeline timeline = Timeline.Create().Build();
        ITimelineRunBuilder builder = timeline.SetupRun(new DebuggerServiceProvider(debugger));

        await Task.Delay(10);
        await builder.RunAsync();

        AssertIdentifiedAs(debugger, nameof(ResolvesWhenTheBuilderIsHeldBeforeBeingRun));
    }

    private async Task StartAfterAwaitingAsync(IRunDebugger debugger)
    {
        await Task.Delay(10);

        Timeline timeline = Timeline.Create().Build();
        await timeline.SetupRun(new DebuggerServiceProvider(debugger)).RunAsync();
    }

    private async Task StartViaHelperAsync(IRunDebugger debugger)
    {
        Timeline timeline = Timeline.Create().Build();
        await timeline.SetupRun(new DebuggerServiceProvider(debugger)).RunAsync();
    }

    private static void AssertIdentifiedAs(IdentityRecordingDebugger debugger, string expectedMethod)
    {
        TestIdentity identity = Assert.IsType<TestIdentity>(debugger.Identity);

        Assert.Equal(TestFrameworkKind.XUnit, identity.Framework);
        Assert.Equal(expectedMethod, identity.MethodName);
        Assert.Equal($"{typeof(TestIdentityNestingTests).FullName}.{expectedMethod}", identity.FullyQualifiedName);
    }

    private sealed class DebuggerServiceProvider(IRunDebugger debugger) : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => serviceType == typeof(IRunDebugger) ? debugger : null;
    }

    private sealed class IdentityRecordingDebugger : IRunDebugger
    {
        public TestIdentity? Identity { get; private set; }

        public bool IsCapturing => true;

        public Task SignalInitTimelineRunAsync(string sessionId, string name, string projectPath, TimelineRunStructure runStructure, TestIdentity? identity = null)
        {
            Identity = identity;
            return Task.CompletedTask;
        }

        public Task SignalEntityTransitionAsync(string sessionId, DebugEntityKind entityKind, string? stage, int? stepId, DebugLifecycleState state, DebugLifecycleState? previousState = null, DebugLifecycleState? outcomeState = null, DebugFailureDetail? failure = null) => Task.CompletedTask;
        public Task SignalValueUpdateAsync(string sessionId, string name, DebugValueKind valueKind, string? stage, int? stepId, DebugValueEnvelope value) => Task.CompletedTask;
        public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry) => Task.CompletedTask;
        public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry) => Task.CompletedTask;
        public Task SignalTimelineRunFinishedAsync(string sessionId) => Task.CompletedTask;
        public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId) => Task.CompletedTask;
    }
}
