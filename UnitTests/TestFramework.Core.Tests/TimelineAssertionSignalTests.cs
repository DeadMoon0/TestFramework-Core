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
            .SetVariable("name", "Ada")
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
            .SetVariable("name", "Grace")
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
            .SetVariable("name", "Ada")
            .Build();

        await timeline.SetupRun(new DebuggerServiceProvider(debugger)).RunAsync();

        Assert.Empty(debugger.Assertions);
    }

    [Fact]
    public async Task ACheckOnTheFinishedRunIsReportedAfterTheFinish()
    {
        // The usual place for a check: on what RunAsync handed back, after the run has said it is done.
        LateAssertionDebugger debugger = new();

        TimelineRun run = await Timeline.Create()
            .SetVariable("count", 6)
            .Build()
            .SetupRun(new DebuggerServiceProvider(debugger))
            .RunAsync();

        run.Variable<int>("count").Should().Be(6);

        Assert.Equal(["finished", "late assertion"], debugger.Heard);
        DebugAssertionEntry assertion = Assert.Single(debugger.LateAssertions);
        Assert.True(assertion.Succeeded);
        Assert.Contains("count", assertion.Target, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFailedCheckOnTheFinishedRunIsReportedBeforeItThrows()
    {
        LateAssertionDebugger debugger = new();

        TimelineRun run = await Timeline.Create()
            .SetVariable("count", 6)
            .Build()
            .SetupRun(new DebuggerServiceProvider(debugger))
            .RunAsync();

        Assert.ThrowsAny<Exception>(() => run.Variable<int>("count").Should().Be(7));

        DebugAssertionEntry assertion = Assert.Single(debugger.LateAssertions);
        Assert.False(assertion.Succeeded);
        Assert.Contains("6", assertion.Actual.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADebuggerThatTakesNoLateChecksHearsNothingAfterTheFinish()
    {
        // A debugger written against IRunDebugger may rely on "finished" being the last thing it hears.
        AssertionRecordingDebugger debugger = new();

        TimelineRun run = await Timeline.Create()
            .SetVariable("count", 6)
            .Build()
            .SetupRun(new DebuggerServiceProvider(debugger))
            .RunAsync();

        run.Variable<int>("count").Should().Be(6);

        Assert.Empty(debugger.Assertions);
    }

    private sealed class LateAssertionDebugger : IRunDebugger, ISupportsLateAssertions
    {
        private readonly List<string> heard = [];
        private readonly List<DebugAssertionEntry> lateAssertions = [];

        internal IReadOnlyList<string> Heard => heard;

        internal IReadOnlyList<DebugAssertionEntry> LateAssertions => lateAssertions;

        public Task SignalInitTimelineRunAsync(string sessionId, DebugRunStart start) => Task.CompletedTask;

        public Task SignalEntityTransitionAsync(string sessionId, DebugEntityTransition transition) => Task.CompletedTask;

        public Task SignalValueUpdateAsync(string sessionId, DebugValueUpdate update) => Task.CompletedTask;

        public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry) => Task.CompletedTask;

        public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry)
        {
            heard.Add("assertion");
            return Task.CompletedTask;
        }

        public Task SignalLateAssertionAsync(string sessionId, DebugAssertionEntry entry)
        {
            heard.Add("late assertion");
            lateAssertions.Add(entry);
            return Task.CompletedTask;
        }

        public Task SignalTimelineRunFinishedAsync(string sessionId)
        {
            heard.Add("finished");
            return Task.CompletedTask;
        }

        public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId) => Task.CompletedTask;
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

        public Task SignalInitTimelineRunAsync(string sessionId, DebugRunStart start) => Task.CompletedTask;

        public Task SignalEntityTransitionAsync(string sessionId, DebugEntityTransition transition) => Task.CompletedTask;

        public Task SignalValueUpdateAsync(string sessionId, DebugValueUpdate update) => Task.CompletedTask;

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
