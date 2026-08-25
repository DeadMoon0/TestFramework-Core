using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;
using Xunit;
using Xunit.Abstractions;

namespace TestFramework.Core.Tests;

/// <summary>
/// What a step is now given, and what it can do with it that it could not before.
/// </summary>
/// <remarks>
/// <para>
/// A step used to receive four stores and a cancellation token, which left it unable to answer two
/// questions it kept needing: how long do I have, and where did this resource end up. Packages answered
/// both themselves - two of them hand-rolled their own timeout margins, and each grew its own way of
/// finding an address, which is what the bridges between them were for.
/// </para>
/// <para>
/// Both answers now arrive by the same route, so these tests are about the route rather than about any
/// one step.
/// </para>
/// </remarks>
[Collection(nameof(RunContextTests))]
public class RunContextTests(ITestOutputHelper output) : IDisposable
{
    private const string SqlKind = "test.runcontext.sql";

    public void Dispose() => ResourceKindRegistry.Reset();

    [Fact]
    public async Task AStepIsToldHowLongItHasRatherThanGuessing()
    {
        // The gap that made two packages under-cut their deadlines by hand: a step could be cancelled but
        // never told when that would happen, so anything wanting a useful timeout message invented a
        // margin. A step that knows what remains can decide for itself.
        // The recorder is shared rather than the step: a run executes a *clone* of the step it was given,
        // so anything the step wants to report has to live outside it. Returning 'this' from Clone() would
        // be the shortcut, and the framework refuses it.
        Recorder recorder = new Recorder();

        Timeline timeline = Timeline.Create()
            .Trigger(new DeadlineReadingStep(recorder)).WithTimeOut(TimeSpan.FromSeconds(30)).Name("reads-its-deadline")
            .Build();

        TimelineRun run = await timeline.SetupRun(outputHelper: output).RunAsync();

        Assert.Equal(StepState.Complete, run.Step("reads-its-deadline").LastResult.State);

        ObservedDeadline observed = Assert.IsType<ObservedDeadline>(recorder.Observed);

        Assert.Equal(TimeSpan.FromSeconds(30), observed.Total);
        Assert.InRange(observed.Remaining, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(30));
        Assert.False(observed.IsUnbounded);

        // The token it is cancelled with belongs to the same deadline, so the two cannot disagree.
        Assert.True(observed.HasToken);
    }

    [Fact]
    public async Task AStepReadsWhereTheRunsResourcesEndedUp()
    {
        // One question, one answer, whoever supplied it. This run's resource was declared rather than
        // started, and the step cannot tell the difference - which is the point.
        ResourceKind sql = ResourceKind.Named(SqlKind).Offers(ValueNames.ConnectionString).Build();

        Recorder recorder = new Recorder();

        Timeline timeline = Timeline.Create()
            .Trigger(new ValueReadingStep(ValueRef.For(sql.Name, "orders-db", ValueNames.ConnectionString), recorder)).Name("reads-a-value")
            .Build();

        TimelineRun run = await timeline
            .SetupRun(new SourceProvider(new DeclaringSource(sql, "orders-db", "Server=declared")), output)
            .RunAsync();

        Assert.Equal(StepState.Complete, run.Step("reads-a-value").LastResult.State);
        Assert.Equal("Server=declared", recorder.Observed);

        // And the run keeps the same answer, so an assertion after the fact reads what the step read.
        Assert.Equal("Server=declared", run.Values.Require(ValueRef.For(sql.Name, "orders-db", ValueNames.ConnectionString), ResourceVantage.Host));
    }

    [Fact]
    public async Task AValueNoResourceOffersStopsTheRunBeforeAnyStepStarts()
    {
        // Plan time, not run time: the alternative is a step waiting out its timeout against an address
        // nobody ever supplied, which is the same failure with none of the information.
        ResourceKind sql = ResourceKind.Named(SqlKind).Offers(ValueNames.ConnectionString).Build();

        Recorder recorder = new Recorder();

        Timeline timeline = Timeline.Create()
            .Trigger(new CountingStep(recorder)).Name("never-runs")
            .Build();

        FrameworkConfigurationException failure = await Assert.ThrowsAsync<FrameworkConfigurationException>(
            () => timeline
                .SetupRun(new SourceProvider(new MisdeclaringSource(sql, "orders-db")), output)
                .RunAsync());

        Assert.Contains("does not offer", failure.Message, StringComparison.Ordinal);
        Assert.Equal(0, recorder.Runs);
    }

    [Fact]
    public async Task AStepCanBeDrivenOnItsOwnWithoutATimeline()
    {
        // What a package author does to unit-test their own step. Public API only - no run, no timeline,
        // and an empty resolution that says plainly that nothing here supplies a resource.
        ScopedLogger logger = new ScopedLogger(null);
        DebuggingRunSession session = new DebuggingRunSession(new EmptyRunDebugger());

        RunContext context = RunContext.Ambient(
            new EmptyServices(),
            new VariableStore(logger, session),
            new ArtifactStore(logger, session),
            logger,
            ValueResolution.Empty);

        Recorder recorder = new Recorder();

        await new CountingStep(recorder).Execute(context);

        Assert.Equal(1, recorder.Runs);

        // Unbounded rather than a number somebody invented, and no attempt to quarantine.
        Assert.True(context.Deadline.IsUnbounded);
        Assert.Null(context.Attempt);
    }

    private sealed record ObservedDeadline(TimeSpan Total, TimeSpan Remaining, bool IsUnbounded, bool HasToken);

    /// <summary>
    /// What the step that actually ran saw. Lives outside the step because the run executes a clone.
    /// </summary>
    private sealed class Recorder
    {
        public object? Observed { get; private set; }

        public int Runs { get; private set; }

        public void Record(object observed)
        {
            this.Observed = observed;
            this.Runs++;
        }
    }

    private sealed class DeadlineReadingStep(Recorder recorder) : Step<EmptyStepResultContext>
    {
        public override string Name => "Reads its deadline";

        public override string Description => "Records what it was told about its own time.";

        public override bool DoesReturn => false;

        public override Step<EmptyStepResultContext> Clone() => new DeadlineReadingStep(recorder).WithClonedOptions(this);

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
        {
            recorder.Record(new ObservedDeadline(
                context.Deadline.Total,
                context.Deadline.Remaining,
                context.Deadline.IsUnbounded,
                context.Deadline.Token.CanBeCanceled));

            return Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
        }
    }

    private sealed class ValueReadingStep(ValueRef value, Recorder recorder) : Step<EmptyStepResultContext>
    {
        public override string Name => "Reads a value";

        public override string Description => "Asks where a resource ended up.";

        public override bool DoesReturn => false;

        public override Step<EmptyStepResultContext> Clone() => new ValueReadingStep(value, recorder).WithClonedOptions(this);

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
        {
            recorder.Record(context.Values.Require(value, ResourceVantage.Host));

            return Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
        }
    }

    private sealed class CountingStep(Recorder recorder) : Step<EmptyStepResultContext>
    {
        public override string Name => "Counts";

        public override string Description => "Records that it ran at all.";

        public override bool DoesReturn => false;

        public override Step<EmptyStepResultContext> Clone() => new CountingStep(recorder).WithClonedOptions(this);

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
        {
            recorder.Record("ran");

            return Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
        }
    }

    /// <summary>A piece that says what it found, the way a configuration file or a fixture does.</summary>
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

    /// <summary>Declares a value its own kind never offered.</summary>
    private sealed class MisdeclaringSource(ResourceKind kind, string identifier) : DeclaredNodeSource
    {
        public override string SourceName => "a section somebody mistyped";

        protected override IEnumerable<DeclaredResource> Declarations =>
        [
            new DeclaredResource(
                kind,
                identifier,
                new Dictionary<ValueKey, string> { [new ValueKey(ValueNames.BaseUrl)] = "http://localhost:1/" },
                this.SourceName),
        ];
    }

    private sealed class SourceProvider(IResourceNodeSource source) : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => serviceType == typeof(IEnumerable<IResourceNodeSource>) ? new[] { source } : null;
    }

    private sealed class EmptyServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
