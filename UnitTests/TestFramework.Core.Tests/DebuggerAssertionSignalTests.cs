using TestFramework.Core.Debugger;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;
using TestFramework.Core.Timelines.Assertions;

namespace TestFramework.Core.Tests;

public class DebuggerAssertionSignalTests
{
    [Fact]
    public void ValueAssertion_EmitsStructuredAssertionSignal()
    {
        RecordingRunDebugger debugger = new();
        ScopedLogger logger = ScopedLogger.CreateWithDebuggerSession(new DebuggingRunSession(debugger));

        Assert.Throws<ValueAssertionException>(() => new ValueAsserter<string>("Grace", "user", logger).Be("Ada"));

        DebugAssertionEntry assertion = Assert.Single(debugger.Assertions);
        Assert.Equal(DebugAssertionTargetKind.Value, assertion.TargetKind);
        Assert.Equal("user", assertion.Target);
        Assert.Equal("Be", assertion.AssertionName);
        Assert.Equal("Ada", assertion.Expected);
        Assert.Equal("Grace", assertion.Actual);
        Assert.False(assertion.Succeeded);
        Assert.Equal("expected \"Ada\", was \"Grace\"", assertion.FailureReason);
    }

    [Fact]
    public void LogEntry_WithoutStepIterationContext_Throws()
    {
        RecordingRunDebugger debugger = new();
        ScopedLogger logger = ScopedLogger.CreateWithDebuggerSession(new DebuggingRunSession(debugger));

        Assert.Throws<InvalidOperationException>(() => logger.LogInformation("outside iteration"));
    }

    private sealed class RecordingRunDebugger : IRunDebugger
    {
        internal List<DebugAssertionEntry> Assertions { get; } = [];

        public Task SignalInitTimelineRunAsync(string sessionId, string name, string projectPath, TimelineRunStructure runStructure) => Task.CompletedTask;
        public Task SignalEntityTransitionAsync(string sessionId, DebugEntityKind entityKind, string? stage, int? stepId, DebugLifecycleState state, DebugLifecycleState? previousState = null, DebugLifecycleState? outcomeState = null) => Task.CompletedTask;
        public Task SignalValueUpdateAsync(string sessionId, string name, DebugValueKind valueKind, string? stage, int? stepId, DebugValueEnvelope value) => Task.CompletedTask;
        public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry) => Task.CompletedTask;
        public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry)
        {
            Assertions.Add(entry);
            return Task.CompletedTask;
        }
        public Task SignalTimelineRunFinishedAsync(string sessionId) => Task.CompletedTask;
        public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId) => Task.CompletedTask;
    }
}