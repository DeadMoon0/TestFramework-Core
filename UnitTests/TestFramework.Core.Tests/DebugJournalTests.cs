using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

/// <summary>
/// Serialises the classes that mutate debugger-related environment variables. Without this they can
/// observe each other's settings and fail in ways that depend on scheduling.
/// </summary>
[CollectionDefinition("DebuggerEnvironment", DisableParallelization = true)]
public sealed class DebuggerEnvironmentCollection;

[Collection("DebuggerEnvironment")]
public sealed class DebugJournalTests : IDisposable
{
    private readonly string journalRoot = Path.Combine(Path.GetTempPath(), "tf-journal-tests", Guid.NewGuid().ToString("N"));
    private readonly string? previousDir = System.Environment.GetEnvironmentVariable("TESTFRAMEWORK_DEBUG_JOURNAL_DIR");
    private readonly string? previousKeep = System.Environment.GetEnvironmentVariable("TESTFRAMEWORK_DEBUG_JOURNAL_KEEP");

    public DebugJournalTests()
    {
        System.Environment.SetEnvironmentVariable("TESTFRAMEWORK_DEBUG_JOURNAL_DIR", journalRoot);
        DebugJournal.ResetForTests();
    }

    public void Dispose()
    {
        System.Environment.SetEnvironmentVariable("TESTFRAMEWORK_DEBUG_JOURNAL_DIR", previousDir);
        System.Environment.SetEnvironmentVariable("TESTFRAMEWORK_DEBUG_JOURNAL_KEEP", previousKeep);
        DebugJournal.ResetForTests();

        try
        {
            if (Directory.Exists(journalRoot))
                Directory.Delete(journalRoot, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a test over.
        }
    }

    private void ArmJournal()
    {
        Directory.CreateDirectory(journalRoot);
        DebugJournal.ResetForTests();
    }

    [Fact]
    public async Task WithoutTheMarkerDirectoryNothingIsWrittenAtAll()
    {
        // The load-bearing promise: a machine that never installed the UI must not pay for it. The
        // marker is absent here, so the run must not create it or anything under it.
        Assert.False(Directory.Exists(journalRoot));
        Assert.False(DebugJournal.IsEnabled);

        JournalRunDebugger debugger = new();
        Assert.False(debugger.IsCapturing);

        await EmitRunAsync(debugger, "session-quiet");
        debugger.Dispose();

        Assert.False(Directory.Exists(journalRoot));
    }

    [Fact]
    public async Task WithTheMarkerDirectoryARunIsRecorded()
    {
        ArmJournal();

        JournalRunDebugger debugger = new();
        Assert.True(debugger.IsCapturing);

        await EmitRunAsync(debugger, "session-1");
        debugger.Dispose();

        Assert.Single(JournalFiles());
        Assert.Single(MetadataFiles());
    }

    [Fact]
    public async Task EveryJournalLineIsAnEnvelopeThatReplaysToItsSignal()
    {
        // Journal lines and pipe frames are the same envelopes, which is what lets a replayed run
        // take the live code path instead of needing a second parser.
        ArmJournal();

        JournalRunDebugger debugger = new();
        await EmitRunAsync(debugger, "session-2");
        debugger.Dispose();

        string[] lines = ReadLines(JournalFiles().Single());
        Assert.NotEmpty(lines);

        List<PipeSignalKind> kinds = [];
        long previousSequence = 0;

        foreach (string line in lines)
        {
            DebugEnvelope envelope = DebugEnvelopeCodec.Deserialize(line);

            Assert.Equal(DebugProtocol.Version, envelope.V);
            Assert.Equal("session-2", envelope.SessionId);
            Assert.True(envelope.Seq > previousSequence, "Sequence numbers must increase monotonically.");
            previousSequence = envelope.Seq;

            IPipeSignal signal = DebugEnvelopeCodec.Unwrap(envelope);
            Assert.Equal(envelope.Kind, signal.Kind);
            kinds.Add(signal.Kind);
        }

        Assert.Equal(PipeSignalKind.InitTimelineRun, kinds[0]);
        Assert.Equal(PipeSignalKind.TimelineRunFinished, kinds[^1]);
        Assert.Contains(PipeSignalKind.EntityTransition, kinds);
        Assert.Contains(PipeSignalKind.ValueUpdate, kinds);
    }

    [Fact]
    public async Task AskingWhetherToPauseIsNotRecordedAsSomethingThatHappened()
    {
        // Every step asks permission before it runs, so recording the question would put one line
        // per step into the journal describing a pause that never occurred - doubling the file and
        // making a replayed run show each step as briefly held. Driven by a real timeline, because
        // the per-step asking is the framework's behaviour rather than this test's.
        ArmJournal();

        Timeline timeline = Timeline.Create()
            .Trigger(new JournalNoopStep())
            .Name("first")
            .Trigger(new JournalNoopStep())
            .Name("second")
            .Build();

        await timeline.SetupRun().RunAsync();

        string[] lines = ReadLines(JournalFiles().Single());
        Assert.NotEmpty(lines);

        Assert.DoesNotContain(
            lines.Select(DebugEnvelopeCodec.Deserialize),
            envelope => envelope.Kind == PipeSignalKind.BreakpointHitRequest);
    }

    [Fact]
    public async Task MetadataDescribesTheRunWithoutParsingTheJournal()
    {
        ArmJournal();

        JournalRunDebugger debugger = new();
        await EmitRunAsync(debugger, "session-3", name: "My Test", projectPath: "some/project.csproj");
        debugger.Dispose();

        DebugRunMetadata metadata = ReadMetadata(MetadataFiles().Single());

        Assert.Equal("session-3", metadata.SessionId);
        Assert.Equal("My Test", metadata.Name);
        Assert.Equal("some/project.csproj", metadata.ProjectPath);
        Assert.Equal(DebugProtocol.Version, metadata.ProtocolVersion);
        Assert.Equal(DebugRunOutcome.Finished, metadata.Outcome);
        Assert.NotNull(metadata.FinishedAtUtc);
        Assert.True(metadata.EventCount > 0);
        Assert.Equal(Path.GetFileName(JournalFiles().Single()), metadata.JournalFileName);
    }

    [Fact]
    public async Task MetadataCarriesTheTestIdentityForRerun()
    {
        // The picker offers re-run for a completed run, so the identity has to be in the sidecar
        // rather than only inside the journal.
        ArmJournal();

        JournalRunDebugger debugger = new();
        await debugger.SignalInitTimelineRunAsync(
            "session-identity",
            "Named Run",
            "project.csproj",
            EmptyStructure(),
            new TestIdentity
            {
                DisplayName = "Named Run",
                Framework = TestFrameworkKind.XUnit,
                TypeFullName = "Some.Namespace.Tests",
                MethodName = "Named Run",
                FullyQualifiedName = "Some.Namespace.Tests.NamedRun",
                AssemblyPath = "testhost.exe",
                ProjectFilePath = "some/project.csproj"
            });
        await debugger.SignalTimelineRunFinishedAsync("session-identity");
        debugger.Dispose();

        DebugRunMetadata metadata = ReadMetadata(MetadataFiles().Single());

        Assert.NotNull(metadata.Identity);
        Assert.Equal("Some.Namespace.Tests.NamedRun", metadata.Identity!.FullyQualifiedName);
        Assert.Equal(TestFrameworkKind.XUnit, metadata.Identity.Framework);
        Assert.True(metadata.Identity.CanRerun);
    }

    [Fact]
    public async Task AKilledRunLeavesItsMetadataMarkedRunning()
    {
        // A test host that dies never reaches finish or dispose. The sidecar still says Running,
        // which is how the UI can list the run and show it as aborted rather than silently dropping
        // it — the current UI discards unfinished runs entirely.
        ArmJournal();

        JournalRunDebugger debugger = new();
        await debugger.SignalInitTimelineRunAsync("session-4", "Doomed", "project.csproj", EmptyStructure());
        await debugger.SignalEntityTransitionAsync("session-4", DebugEntityKind.Run, null, null, DebugLifecycleState.Running);

        // Deliberately no finish and no dispose.
        DebugRunMetadata metadata = ReadMetadata(MetadataFiles().Single());

        Assert.Equal(DebugRunOutcome.Running, metadata.Outcome);
        Assert.Null(metadata.FinishedAtUtc);

        // The events written before the kill are on disk, not stuck in a buffer.
        Assert.NotEmpty(ReadLines(JournalFiles().Single()));
    }

    [Fact]
    public async Task RetentionKeepsOnlyTheNewestRuns()
    {
        ArmJournal();
        System.Environment.SetEnvironmentVariable("TESTFRAMEWORK_DEBUG_JOURNAL_KEEP", "3");

        for (int i = 0; i < 6; i++)
        {
            JournalRunDebugger debugger = new();
            await EmitRunAsync(debugger, $"session-{i:D2}");
            debugger.Dispose();

            // The file stem is timestamped to the millisecond; keep the runs distinguishable.
            await Task.Delay(5);
        }

        // Three retained plus the one just written, which is pruned on the *next* run's start.
        Assert.InRange(MetadataFiles().Count(), 3, 4);
        Assert.Equal(MetadataFiles().Count(), JournalFiles().Count());
    }

    [Fact]
    public async Task AnUnwritableJournalDoesNotFailTheRun()
    {
        ArmJournal();

        // A file where the runs directory should be: creating the directory will fail.
        File.WriteAllText(Path.Combine(journalRoot, "runs"), "not a directory");

        JournalRunDebugger debugger = new();
        await EmitRunAsync(debugger, "session-5");
        debugger.Dispose();
    }

    [Fact]
    public void CommonDebuggerIncludesTheJournalOnlyWhenTheMarkerExists()
    {
        string? previousName = System.Environment.GetEnvironmentVariable("TESTFRAMEWORK_DEBUG_PIPE_NAME");
        try
        {
            // Isolate from any real DebugUI so only the journal decides the outcome.
            System.Environment.SetEnvironmentVariable("TESTFRAMEWORK_DEBUG_PIPE_NAME", $"testframework-absent-{Guid.NewGuid():N}");
            PipeClient.ResetAvailabilityForTests();

            Assert.IsType<EmptyRunDebugger>(CommonDebugger.GetCommon());

            ArmJournal();

            Assert.IsType<JournalRunDebugger>(CommonDebugger.GetCommon());
        }
        finally
        {
            System.Environment.SetEnvironmentVariable("TESTFRAMEWORK_DEBUG_PIPE_NAME", previousName);
            PipeClient.ResetAvailabilityForTests();
        }
    }

    private static async Task EmitRunAsync(JournalRunDebugger debugger, string sessionId, string name = "Run", string projectPath = "project.csproj")
    {
        await debugger.SignalInitTimelineRunAsync(sessionId, name, projectPath, EmptyStructure());
        await debugger.SignalEntityTransitionAsync(sessionId, DebugEntityKind.Run, null, null, DebugLifecycleState.Running);
        await debugger.SignalValueUpdateAsync(sessionId, "answer", DebugValueKind.Variable, "Main", 0, new DebugValueEnvelope
        {
            Kind = DebugValueKind.Variable,
            TypeName = "System.Int32",
            Description = new DebugValueDescription { Summary = "42", Shape = DebugValueShape.Scalar },
            SchemaKey = "tf.variable:System.Int32"
        });
        await debugger.SignalEntityTransitionAsync(sessionId, DebugEntityKind.Run, null, null, DebugLifecycleState.Complete);
        await debugger.SignalTimelineRunFinishedAsync(sessionId);
    }

    private static TimelineRunStructure EmptyStructure() => new()
    {
        Variables = new Dictionary<VariableIdentifier, DebugValue>(),
        Artifacts = new Dictionary<ArtifactIdentifier, DebugValue>(),
        Stages = []
    };

    private static DebugRunMetadata ReadMetadata(string path)
    {
        using StreamReader reader = DebugJournal.OpenForReading(path);
        return JsonConvert.DeserializeObject<DebugRunMetadata>(reader.ReadToEnd())
               ?? throw new InvalidOperationException("Could not read run metadata.");
    }

    /// <summary>Reads a journal that a run may still be writing.</summary>
    private static string[] ReadLines(string path)
    {
        using StreamReader reader = DebugJournal.OpenForReading(path);
        List<string> lines = [];

        while (reader.ReadLine() is string line)
        {
            if (line.Length > 0)
                lines.Add(line);
        }

        return [.. lines];
    }

    private IEnumerable<string> JournalFiles()
    {
        string runs = Path.Combine(journalRoot, "runs");
        return Directory.Exists(runs) ? Directory.EnumerateFiles(runs, "*.ndjson") : [];
    }

    private IEnumerable<string> MetadataFiles()
    {
        string runs = Path.Combine(journalRoot, "runs");
        return Directory.Exists(runs) ? Directory.EnumerateFiles(runs, "*.meta.json") : [];
    }

    private sealed class JournalNoopStep : Step<EmptyStepResultContext>
    {
        public override string Name => "noop";
        public override string Description => "Does nothing.";
        public override bool DoesReturn => false;

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
            => Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);

        public override Step<EmptyStepResultContext> Clone() => new JournalNoopStep().WithClonedOptions(this);
        public override void DeclareIO(StepIOContract contract) { }
        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }
}
