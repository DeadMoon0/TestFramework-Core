using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

/// <summary>
/// Covers when a variable write is reported to a debugger and when it is suppressed as unchanged.
/// </summary>
/// <remarks>
/// The rule used to be the *display* text, which <see cref="VariableFormatter"/> truncates at 120
/// characters. Two different values sharing a 117-character prefix therefore looked identical and
/// the second write was dropped — a silent loss on exactly the large payloads someone is most
/// likely to be inspecting.
/// </remarks>
public sealed class VariableChangeDetectionTests
{
    [Fact]
    public async Task ValuesDifferingOnlyBeyondTheDisplayCutOffAreReported()
    {
        // The regression this exists for. Both format to the same truncated text.
        string shared = new('x', 200);

        RecordingDebugger debugger = await RunWritesAsync(store =>
        {
            store.SetVariable("payload", shared + "-first");
            store.SetVariable("payload", shared + "-second");
        });

        Assert.Equal(2, debugger.UpdatesFor("payload"));
    }

    [Fact]
    public void DisplayFormattingStillTruncates()
    {
        // Guards against "fixing" the dedupe rule by removing truncation from the display path,
        // which would push megabyte payloads through every log line.
        string long_ = new('x', 500);

        Assert.True(VariableFormatter.Format(long_).Length < 200);
    }

    [Fact]
    public async Task WritingTheSameValueTwiceIsReportedOnce()
    {
        RecordingDebugger debugger = await RunWritesAsync(store =>
        {
            store.SetVariable("count", 7);
            store.SetVariable("count", 7);
        });

        Assert.Equal(1, debugger.UpdatesFor("count"));
    }

    [Fact]
    public async Task WritingADifferentValueIsReported()
    {
        RecordingDebugger debugger = await RunWritesAsync(store =>
        {
            store.SetVariable("count", 7);
            store.SetVariable("count", 8);
        });

        Assert.Equal(2, debugger.UpdatesFor("count"));
    }

    [Fact]
    public async Task MutatingAReferenceInPlaceIsStillReported()
    {
        // The reason the rule is content-based rather than a value comparison: the same instance
        // written twice is equal to itself, but its contents changed.
        List<string> items = ["one"];

        RecordingDebugger debugger = await RunWritesAsync(store =>
        {
            store.SetVariable("items", items);
            items.Add("two");
            store.SetVariable("items", items);
        });

        Assert.Equal(2, debugger.UpdatesFor("items"));
    }

    [Fact]
    public async Task SeparateVariablesDoNotShareAChangeRule()
    {
        RecordingDebugger debugger = await RunWritesAsync(store =>
        {
            store.SetVariable("a", 1);
            store.SetVariable("b", 1);
        });

        Assert.Equal(1, debugger.UpdatesFor("a"));
        Assert.Equal(1, debugger.UpdatesFor("b"));
    }

    [Fact]
    public void ChangeTokensDistinguishValuesTheDisplayFormCannot()
    {
        string shared = new('y', 300);

        Assert.Equal(VariableFormatter.Format(shared + "-a"), VariableFormatter.Format(shared + "-b"));
        Assert.NotEqual(VariableFormatter.CreateChangeToken(shared + "-a"), VariableFormatter.CreateChangeToken(shared + "-b"));
    }

    [Fact]
    public void ChangeTokenIsStableForEqualContent()
        => Assert.Equal(VariableFormatter.CreateChangeToken(new[] { 1, 2, 3 }), VariableFormatter.CreateChangeToken(new[] { 1, 2, 3 }));

    [Fact]
    public void NothingIsComputedWhenNobodyIsCapturing()
    {
        // The capture gate has to come before the fingerprint, or every run pays to serialize every
        // value it ever writes.
        VariableStore store = new(new ScopedLogger(null), new DebuggingRunSession(new EmptyRunDebugger()));

        store.SetVariable("value", new ThrowsWhenSerialized());
    }

    /// <summary>
    /// Drives writes through a real session: value updates are gated on the session being
    /// initialised and are delivered through its queue, so both have to happen for anything to be
    /// observable.
    /// </summary>
    private static async Task<RecordingDebugger> RunWritesAsync(Action<VariableStore> writes)
    {
        RecordingDebugger debugger = new();
        DebuggingRunSession session = new(debugger);
        await session.InitSessionAsync(EmptyStructure());

        VariableStore store = new(new ScopedLogger(null), session);
        writes(store);

        await session.FinishSessionAsync();
        return debugger;
    }

    private static TimelineRunStructure EmptyStructure() => new()
    {
        Variables = new Dictionary<VariableIdentifier, VariableState>(),
        Artifacts = new Dictionary<ArtifactIdentifier, TestFramework.Core.Debugger.ArtifactState>(),
        Stages = []
    };

    /// <summary>Serializing this throws, so touching it proves the capture gate was bypassed.</summary>
    private sealed class ThrowsWhenSerialized
    {
        public string Boom => throw new InvalidOperationException("The capture gate should have prevented this.");
    }

    private sealed class RecordingDebugger : IRunDebugger
    {
        private readonly List<string> names = [];

        public bool IsCapturing => true;

        public int UpdatesFor(string name)
        {
            lock (names) return names.Count(n => n == name);
        }

        public Task SignalValueUpdateAsync(string sessionId, string name, DebugValueKind valueKind, string? stage, int? stepId, DebugValueEnvelope value)
        {
            if (valueKind == DebugValueKind.Variable)
            {
                lock (names) names.Add(name);
            }

            return Task.CompletedTask;
        }

        public Task SignalInitTimelineRunAsync(string sessionId, string name, string projectPath, TimelineRunStructure runStructure, TestIdentity? identity = null) => Task.CompletedTask;
        public Task SignalEntityTransitionAsync(string sessionId, DebugEntityKind entityKind, string? stage, int? stepId, DebugLifecycleState state, DebugLifecycleState? previousState = null, DebugLifecycleState? outcomeState = null, DebugFailureDetail? failure = null) => Task.CompletedTask;
        public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry) => Task.CompletedTask;
        public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry) => Task.CompletedTask;
        public Task SignalTimelineRunFinishedAsync(string sessionId) => Task.CompletedTask;
        public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId) => Task.CompletedTask;
    }
}
