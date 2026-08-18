using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TestFramework.Core.Debugger;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

/// <summary>
/// Covers assertions written into a timeline reaching the debugger as assertions.
/// </summary>
/// <remarks>
/// A consumer decides whether a run proved anything from the assertions it was told about. An
/// assertion written in the builder is a step, so before this it reported nothing at all and a
/// timeline full of checks was indistinguishable from one that checked nothing.
/// </remarks>
public sealed class TimelineAssertionSignalTests
{
    [Fact]
    public async Task AnAssertionThatHoldsIsReported()
    {
        AssertionRecordingDebugger debugger = new();

        Timeline timeline = Timeline.Create()
            .SetVariable("name", Var.Const("Ada"))
            .AssertVariable(Var.Ref<string>("name"), name => name == "Ada")
            .Build();

        await timeline.SetupRun(new DebuggerServiceProvider(debugger)).RunAsync();

        DebugAssertionEntry assertion = Assert.Single(debugger.Assertions);
        Assert.True(assertion.Succeeded);
        Assert.Equal(DebugAssertionTargetKind.Variable, assertion.TargetKind);
        Assert.Equal("name", assertion.Target);
        Assert.Contains("Ada", assertion.Actual.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAssertionThatFailsIsReportedBeforeItThrows()
    {
        // Reported rather than only thrown: the failure is what the reader most needs to see, and a
        // step that only throws leaves the consumer to infer the assertion from the exception type.
        AssertionRecordingDebugger debugger = new();

        Timeline timeline = Timeline.Create()
            .SetVariable("name", Var.Const("Grace"))
            .AssertVariable(Var.Ref<string>("name"), name => name == "Ada")
            .Build();

        TimelineRun run = await timeline.SetupRun(new DebuggerServiceProvider(debugger)).RunAsync();

        Assert.Throws<TimelineRunFailedException>(run.EnsureRanToCompletion);

        DebugAssertionEntry assertion = Assert.Single(debugger.Assertions);
        Assert.False(assertion.Succeeded);
        Assert.Contains("Grace", assertion.Actual.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARunWithNoAssertionsReportsNone()
    {
        // The other half of the claim: "nothing asserted" has to keep meaning nothing was asserted.
        AssertionRecordingDebugger debugger = new();

        Timeline timeline = Timeline.Create()
            .SetVariable("name", Var.Const("Ada"))
            .Build();

        await timeline.SetupRun(new DebuggerServiceProvider(debugger)).RunAsync();

        Assert.Empty(debugger.Assertions);
    }

    private sealed class DebuggerServiceProvider(IRunDebugger debugger) : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => serviceType == typeof(IRunDebugger) ? debugger : null;
    }

    private sealed class AssertionRecordingDebugger : IRunDebugger
    {
        private readonly List<DebugAssertionEntry> assertions = [];

        internal IReadOnlyList<DebugAssertionEntry> Assertions => assertions;

        public Task SignalInitTimelineRunAsync(string sessionId, string name, string projectPath, TimelineRunStructure runStructure, TestIdentity? identity = null) => Task.CompletedTask;

        public Task SignalEntityTransitionAsync(string sessionId, DebugEntityKind entityKind, string? stage, int? stepId, DebugLifecycleState state, DebugLifecycleState? previousState = null, DebugLifecycleState? outcomeState = null, DebugFailureDetail? failure = null) => Task.CompletedTask;

        public Task SignalValueUpdateAsync(string sessionId, string name, DebugValueKind valueKind, string? stage, int? stepId, DebugValueEnvelope value) => Task.CompletedTask;

        public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry) => Task.CompletedTask;

        public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry)
        {
            assertions.Add(entry);
            return Task.CompletedTask;
        }

        public Task SignalTimelineRunFinishedAsync(string sessionId) => Task.CompletedTask;

        public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId) => Task.CompletedTask;
    }
}
