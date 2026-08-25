using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using Xunit;
using Xunit.Abstractions;

namespace TestFramework.Core.Tests;

/// <summary>
/// What an artifact's own hooks are given, now that they are given the same thing a step is.
/// </summary>
/// <remarks>
/// <para>
/// Setting an artifact up talks to somebody's database, and it used to be handed four loose parameters
/// with no deadline in them at all: an artifact setup could not know its budget, so it either hung until
/// the whole run gave up or invented a margin of its own. It also had no attempt, which meant a setup
/// running on behalf of an abandoned step wrote to the run's stores as if it were live.
/// </para>
/// <para>
/// Both follow from receiving the step's context rather than pieces of it, which is what these assert.
/// </para>
/// </remarks>
public class ArtifactHookContextTests(ITestOutputHelper output)
{
    [Fact]
    public async Task AnArtifactSetupAndTeardownAreToldWhatTheStepWasTold()
    {
        Recorder recorder = new Recorder();

        Timeline timeline = Timeline.Create()
            .SetupArtifact("data")
            .Build();

        TimelineRun run = await timeline
            .SetupRun(outputHelper: output)
            .AddArtifact("data", new RecordingReference(recorder), new RecordingData())
            .RunAsync();

        run.EnsureRanToCompletion();

        // Setup and teardown both ran, and each was handed a context. There is no resolve here: the data
        // was seeded with the artifact, so nothing had to go and fetch it.
        Assert.Contains("setup", recorder.Seen.Keys);
        Assert.Contains("deconstruct", recorder.Seen.Keys);

        foreach ((string hook, ObservedContext observed) in recorder.Seen)
        {
            // A budget it can read. Ten minutes is the default step timeout, so what matters here is that
            // it is bounded at all - before, an artifact hook had no deadline in its parameters to read.
            Assert.False(observed.IsUnbounded, $"{hook} was given no deadline");
            Assert.True(observed.CanBeCancelled, $"{hook} was given a token nothing can cancel");

            // And an attempt, so anything it writes is quarantined exactly as the step's own writes are.
            Assert.True(observed.HasAttempt, $"{hook} was not told which attempt it belongs to");
        }
    }

    [Fact]
    public async Task AnArtifactHookReadsResourcesTheSameWayAStepDoes()
    {
        // The point of one context: a describer that needs a connection string asks the same question a
        // step asks, instead of the package inventing its own way to find one.
        ResourceKind sql = ResourceKind.Named("test.artifacthook.sql").Offers(ValueNames.ConnectionString).Build();

        try
        {
            Recorder recorder = new Recorder();

            Timeline timeline = Timeline.Create()
                .SetupArtifact("data")
                .Build();

            TimelineRun run = await timeline
                .SetupRun(new SourceProvider(new DeclaringSource(sql, "orders-db", "Server=declared")), output)
                .AddArtifact(
                    "data",
                    new RecordingReference(recorder, ValueRef.For(sql.Name, "orders-db", ValueNames.ConnectionString)),
                    new RecordingData())
                .RunAsync();

            run.EnsureRanToCompletion();

            Assert.Equal("Server=declared", recorder.Seen["setup"].ResolvedValue);
        }
        finally
        {
            ResourceKindRegistry.Reset();
        }
    }

    private sealed record ObservedContext(bool IsUnbounded, bool CanBeCancelled, bool HasAttempt, string? ResolvedValue);

    /// <summary>Collects what each hook saw. Outside the artifact types, because the run clones them.</summary>
    private sealed class Recorder
    {
        private readonly Dictionary<string, ObservedContext> seen = [];

        public IReadOnlyDictionary<string, ObservedContext> Seen => this.seen;

        public void Record(string hook, RunContext context, ValueRef? value)
        {
            string? resolved = value is null ? null : context.Values.Require(value, ResourceVantage.Host);

            lock (this.seen)
            {
                this.seen[hook] = new ObservedContext(
                    context.Deadline.IsUnbounded,
                    context.Deadline.Token.CanBeCanceled,
                    context.Attempt is not null,
                    resolved);
            }
        }
    }

    private sealed class RecordingDescriber : ArtifactDescriber<RecordingDescriber, RecordingData, RecordingReference>
    {
        public override Task Setup(RunContext context, RecordingData data, RecordingReference reference)
        {
            reference.Recorder.Record("setup", context, reference.Value);

            return Task.CompletedTask;
        }

        public override Task Deconstruct(RunContext context, RecordingReference reference)
        {
            reference.Recorder.Record("deconstruct", context, value: null);

            return Task.CompletedTask;
        }

        public override string ToString() => "recording-artifact";
    }

    private sealed class RecordingData : ArtifactData<RecordingData, RecordingDescriber, RecordingReference>
    {
        public override string ToString() => "recording-data";
    }

    private sealed class RecordingReference(Recorder recorder, ValueRef? value = null)
        : ArtifactReference<RecordingReference, RecordingDescriber, RecordingData>
    {

        public Recorder Recorder => recorder;

        public ValueRef? Value => value;



        public override Task<ArtifactResolveResult<RecordingDescriber, RecordingData, RecordingReference>> ResolveToDataAsync(
            RunContext context,
            ArtifactVersionIdentifier versionIdentifier)
        {
            recorder.Record("resolve", context, value: null);

            return Task.FromResult(new ArtifactResolveResult<RecordingDescriber, RecordingData, RecordingReference>
            {
                Found = true,
                Data = new RecordingData(),
            });
        }

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override void OnPinReference(RunContext context)
        {
            // Owned by the run, so teardown deconstructs it instead of passing over it as something the
            // test merely observes - which is what makes Deconstruct reachable here at all.
            this.CanDeconstruct = true;
        }

        public override ArtifactReferenceGeneric CloneForRun()
        {
            // The recorder is shared on purpose: the run works with a copy, and the test reads what the
            // copy saw.
            return new RecordingReference(recorder, value);
        }

        public override string ToString() => "recording-reference";
    }

    private sealed class DeclaringSource(ResourceKind kind, string identifier, string connectionString) : DeclaredNodeSource
    {
        public override string SourceName => "the test's own declarations";

        protected override IEnumerable<DeclaredResource> Declarations =>
        [
            new DeclaredResource(
                kind,
                identifier,
                new Dictionary<ValueKey, string> { [new ValueKey(ValueNames.ConnectionString)] = connectionString },
                this.SourceName),
        ];
    }

    private sealed class SourceProvider(IResourceNodeSource source) : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => serviceType == typeof(IEnumerable<IResourceNodeSource>) ? new[] { source } : null;
    }
}
