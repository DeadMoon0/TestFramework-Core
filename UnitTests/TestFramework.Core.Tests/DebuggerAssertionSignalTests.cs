using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Debugger;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;
using TestFramework.Core.Timelines.Assertions;

namespace TestFramework.Core.Tests;

public class DebuggerAssertionSignalTests
{
    [Fact]
    public async Task ValueAssertion_EmitsStructuredAssertionSignal()
    {
        RecordingRunDebugger debugger = new();
        DebuggingRunSession session = new(debugger);
        ScopedLogger logger = ScopedLogger.CreateWithDebuggerSession(session);

        Assert.Throws<ValueAssertionException>(() => new ValueAsserter<string>("Grace", "user", logger).Be("Ada"));

        // Signals are delivered by a single ordered consumer; finishing the session flushes it.
        await session.FinishSessionAsync();

        DebugAssertionEntry assertion = Assert.Single(debugger.Assertions);
        Assert.Equal(DebugAssertionTargetKind.Value, assertion.TargetKind);
        Assert.Equal("user", assertion.Target);
        Assert.Equal("Be", assertion.AssertionName);

        // The check and its argument, which is the expectation stated rather than rendered into a sentence.
        DebugLogField argument = Assert.Single(assertion.Arguments);
        Assert.Equal("expected", argument.Name);
        Assert.Equal("Ada", (string?)argument.Value);

        // And the value as it actually was, described rather than stringified.
        Assert.Contains("Grace", assertion.Actual.Summary, StringComparison.Ordinal);
        Assert.False(assertion.Succeeded);
    }

    [Fact]
    public async Task LogEntry_WithoutStepIterationContext_EmitsUnscopedLogEntry()
    {
        RecordingRunDebugger debugger = new();
        DebuggingRunSession session = new(debugger);
        ScopedLogger logger = ScopedLogger.CreateWithDebuggerSession(session);

        logger.LogInformation("outside iteration");

        await session.FinishSessionAsync();

        DebugLogEntry entry = Assert.Single(debugger.LogEntries);
        Assert.Equal("outside iteration", DebugLogTemplate.Render(entry));
        Assert.Null(entry.Stage);
        Assert.Null(entry.StepId);
        Assert.Null(entry.Iteration);
    }

    [Fact]
    public async Task LogsAndTransitions_ReachTheDebuggerInTheOrderTheyWereProduced()
    {
        // OutputRunDebugger correlates each log entry against the step that is active when it
        // arrives, so a log that overtakes its step transition is dropped rather than misplaced.
        OrderRecordingRunDebugger debugger = new();
        DebuggingRunSession session = new(debugger);
        ScopedLogger logger = ScopedLogger.CreateWithDebuggerSession(session);

        for (int index = 0; index < 200; index++)
        {
            logger.LogInformation($"log-{index}");
            await session.TransitionStepAsync("Main Stage", index, DebugLifecycleState.Running);
        }

        await session.FinishSessionAsync();

        List<string> expected = [];
        for (int index = 0; index < 200; index++)
        {
            expected.Add($"log:log-{index}");
            expected.Add($"step:{index}");
        }
        expected.Add("finished");

        Assert.Equal(expected, debugger.Events);
    }

    private sealed class OrderRecordingRunDebugger : IRunDebugger
    {
        internal List<string> Events { get; } = [];

        public Task SignalInitTimelineRunAsync(string sessionId, string name, string projectPath, TimelineRunStructure runStructure, TestIdentity? identity = null) => Task.CompletedTask;

        public Task SignalEntityTransitionAsync(string sessionId, DebugEntityKind entityKind, string? stage, int? stepId, DebugLifecycleState state, DebugLifecycleState? previousState = null, DebugLifecycleState? outcomeState = null, DebugFailureDetail? failure = null)
        {
            if (entityKind == DebugEntityKind.Step)
                Events.Add($"step:{stepId}");

            return Task.CompletedTask;
        }

        public Task SignalValueUpdateAsync(string sessionId, string name, DebugValueKind valueKind, string? stage, int? stepId, DebugValueEnvelope value) => Task.CompletedTask;

        public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry)
        {
            Events.Add($"log:{DebugLogTemplate.Render(entry)}");
            return Task.CompletedTask;
        }

        public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry) => Task.CompletedTask;

        public Task SignalTimelineRunFinishedAsync(string sessionId)
        {
            Events.Add("finished");
            return Task.CompletedTask;
        }

        public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId) => Task.CompletedTask;
    }

    private sealed class RecordingRunDebugger : IRunDebugger
    {
        internal List<DebugAssertionEntry> Assertions { get; } = [];
        internal List<DebugLogEntry> LogEntries { get; } = [];

        public Task SignalInitTimelineRunAsync(string sessionId, string name, string projectPath, TimelineRunStructure runStructure, TestIdentity? identity = null) => Task.CompletedTask;
        public Task SignalEntityTransitionAsync(string sessionId, DebugEntityKind entityKind, string? stage, int? stepId, DebugLifecycleState state, DebugLifecycleState? previousState = null, DebugLifecycleState? outcomeState = null, DebugFailureDetail? failure = null) => Task.CompletedTask;
        public Task SignalValueUpdateAsync(string sessionId, string name, DebugValueKind valueKind, string? stage, int? stepId, DebugValueEnvelope value) => Task.CompletedTask;
        public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry)
        {
            LogEntries.Add(entry);
            return Task.CompletedTask;
        }
        public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry)
        {
            Assertions.Add(entry);
            return Task.CompletedTask;
        }
        public Task SignalTimelineRunFinishedAsync(string sessionId) => Task.CompletedTask;
        public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId) => Task.CompletedTask;
    }
}