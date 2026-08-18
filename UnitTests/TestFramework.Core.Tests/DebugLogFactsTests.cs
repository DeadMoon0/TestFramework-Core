using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TestFramework.Core.Debugger;
using TestFramework.Core.Logging;

namespace TestFramework.Core.Tests;

/// <summary>
/// Covers what a log entry carries.
/// </summary>
/// <remarks>
/// The protocol's rule is that facts travel and renderings do not. These are the tests that hold it: an entry
/// carries the values a test logged, typed as they were logged, and the framework's narration of its own
/// progress does not travel at all.
/// </remarks>
public class DebugLogFactsTests
{
    [Fact]
    public async Task AMessageTravelsAsItsTemplateAndItsValues()
    {
        RecordingRunDebugger debugger = new();
        DebuggingRunSession session = new(debugger);
        ScopedLogger logger = ScopedLogger.CreateWithDebuggerSession(session);

        logger.LogInformation("Fetched {0} orders in {1}ms", 4000, 15);

        await session.FinishSessionAsync();

        DebugLogEntry entry = Assert.Single(debugger.LogEntries);

        Assert.Equal("Fetched {0} orders in {1}ms", entry.Template);
        Assert.Equal("InformationLogEvent", entry.EventName);

        // Numbers, not text. A consumer can sort by this, chart it, or compare it against another run — none
        // of which is possible once it has been formatted into the middle of a sentence.
        Assert.Equal(JTokenType.Integer, entry.Fields[0].Value.Type);
        Assert.Equal(4000L, entry.Fields[0].Value.Value<long>());
        Assert.Equal(15L, entry.Fields[1].Value.Value<long>());

        // And the sentence is still one substitution away, for a reader who only wants to read it.
        Assert.Equal("Fetched 4000 orders in 15ms", DebugLogTemplate.Render(entry));
    }

    [Fact]
    public async Task SeverityTravelsWithTheMessage()
    {
        RecordingRunDebugger debugger = new();
        DebuggingRunSession session = new(debugger);
        ScopedLogger logger = ScopedLogger.CreateWithDebuggerSession(session);

        logger.LogInformation("ordinary");
        logger.LogWarning("worth noticing");
        logger.LogError("went wrong");

        await session.FinishSessionAsync();

        Assert.Equal(
            [DebugLogLevel.Information, DebugLogLevel.Warning, DebugLogLevel.Error],
            debugger.LogEntries.Select(entry => entry.Level));
    }

    [Fact]
    public async Task TheFrameworksNarrationOfItselfDoesNotTravel()
    {
        // Entering a step, its result, the stage summary: all of it is on the transport already as lifecycle
        // signals, and the run's plan states each step's phase, label and retry policy. Saying it again in
        // sentences was half of every journal.
        RecordingRunDebugger debugger = new();
        DebuggingRunSession session = new(debugger);
        ScopedLogger logger = ScopedLogger.CreateWithDebuggerSession(session);

        logger.Log(new NarratingEvent());
        logger.LogInformation("this is mine");

        await session.FinishSessionAsync();

        DebugLogEntry entry = Assert.Single(debugger.LogEntries);
        Assert.Equal("this is mine", DebugLogTemplate.Render(entry));
    }

    [Fact]
    public async Task NarrationStillReachesADisplay()
    {
        // The console is the one consumer that wants it, and it gets it in process, rendered.
        RecordingDisplay display = new();
        DebuggingRunSession session = new(display);
        ScopedLogger logger = ScopedLogger.CreateWithDebuggerSession(session);

        logger.Log(new NarratingEvent());

        await session.FinishSessionAsync();

        Assert.Equal("a rule of box-drawing characters", Assert.Single(display.Rendered));
        Assert.Empty(display.LogEntries);
    }

    [Fact]
    public void AValueTooLargeToCarryIsDescribedInstead()
    {
        // A log argument is normally a number. It can also be whatever a test author passed, and an object
        // graph serialised in full would travel down the pipe and into the permanent record of the run.
        DebugLogField field = DebugLogField.Of("payload", new { Blob = new string('x', DebugLogField.MaximumSerializedLength) });

        Assert.Equal(JTokenType.String, field.Value.Type);
        Assert.Contains("too large to carry", field.Value.Value<string>()!, StringComparison.Ordinal);
    }

    [Fact]
    public void AnObjectSmallEnoughToCarryKeepsItsShape()
    {
        DebugLogField field = DebugLogField.Of("order", new { Id = 7, Customer = "Ada" });

        Assert.Equal(JTokenType.Object, field.Value.Type);
        Assert.Equal(7, field.Value["Id"]!.Value<int>());
    }

    [Fact]
    public void AFormatSpecifierStillMeansWhatItMeant()
    {
        // Rendering numbered holes is the framework's own composite formatting applied to the arguments it was
        // given, rather than a re-parse of a sentence somebody already assembled.
        DebugLogFacts facts = DebugLogFacts.Positional("{0:N2} in {1:HH:mm}", 1234.5, new DateTimeOffset(2026, 8, 18, 14, 44, 0, TimeSpan.Zero));

        string rendered = DebugLogTemplate.Render(facts.Template, facts.Fields);

        Assert.Contains(1234.5.ToString("N2", CultureInfo.CurrentCulture), rendered, StringComparison.Ordinal);
        Assert.Contains(new DateTimeOffset(2026, 8, 18, 14, 44, 0, TimeSpan.Zero).ToString("HH:mm", CultureInfo.CurrentCulture), rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void NamedHolesAreFilledByName()
    {
        DebugLogFacts facts = DebugLogFacts.Of("{Step} took {Elapsed}ms", ("Step", "Transform"), ("Elapsed", 15));

        Assert.Equal("Transform took 15ms", DebugLogTemplate.Render(facts.Template, facts.Fields));
    }

    [Fact]
    public void AMessageThatMerelyContainsABraceIsStillShown()
    {
        // A fragment of JSON is not a format string, and refusing to show it would lose the one thing the
        // entry was for.
        DebugLogFacts facts = DebugLogFacts.Positional("{\"id\":1} came back for {0}", "Ada");

        Assert.Equal("{\"id\":1} came back for {0}", DebugLogTemplate.Render(facts.Template, facts.Fields));
    }

    [Fact]
    public void AStringArrivesWithoutItsQuotes()
    {
        Assert.Equal("Ada", DebugLogTemplate.Text(DebugLogField.Of("user", "Ada")));
        Assert.Equal(string.Empty, DebugLogTemplate.Text(DebugLogField.Of("user", null)));
    }

    /// <summary>An event with a console layout and nothing to say to a transport.</summary>
    private sealed class NarratingEvent : LogEvent
    {
        public override DebugLogFacts? Describe() => null;

        public override void FormatLogEvent(LogLineWriter writer) => writer.WriteLine("a rule of box-drawing characters");
    }

    /// <summary>A debugger that only records what the transport carried.</summary>
    private class RecordingRunDebugger : IRunDebugger
    {
        internal List<DebugLogEntry> LogEntries { get; } = [];

        public Task SignalInitTimelineRunAsync(string sessionId, string name, string projectPath, TimelineRunStructure runStructure, TestIdentity? identity = null) => Task.CompletedTask;
        public Task SignalEntityTransitionAsync(string sessionId, DebugEntityKind entityKind, string? stage, int? stepId, DebugLifecycleState state, DebugLifecycleState? previousState = null, DebugLifecycleState? outcomeState = null, DebugFailureDetail? failure = null) => Task.CompletedTask;
        public Task SignalValueUpdateAsync(string sessionId, string name, DebugValueKind valueKind, string? stage, int? stepId, DebugValueEnvelope value) => Task.CompletedTask;

        public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry)
        {
            LogEntries.Add(entry);
            return Task.CompletedTask;
        }

        public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry) => Task.CompletedTask;
        public Task SignalTimelineRunFinishedAsync(string sessionId) => Task.CompletedTask;
        public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId) => Task.CompletedTask;
    }

    /// <summary>The same recorder, but one that displays, and so is offered the rendered lines too.</summary>
    private sealed class RecordingDisplay : RecordingRunDebugger, ISupportsRenderedLog
    {
        internal List<string> Rendered { get; } = [];

        public void WriteRenderedLog(string[] lines, LogPlacement placement) => Rendered.AddRange(lines);
    }
}
