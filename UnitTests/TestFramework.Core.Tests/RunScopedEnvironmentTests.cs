using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Environment;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using Xunit;

namespace TestFramework.Core.Tests;

/// <summary>
/// An environment that keeps what a run decided hands every run an instance of its own.
/// </summary>
public class RunScopedEnvironmentTests
{
    [Fact]
    public async Task TwoRunsHandedOneEnvironment_EachRecordOnlyWhatTheyResolved()
    {
        RecordingEnvironment shared = new();

        TimelineRun first = await Run(shared);
        TimelineRun second = await Run(shared);

        first.EnsureRanToCompletion();
        second.EnsureRanToCompletion();

        RecordingEnvironment firstUsed = Assert.IsType<RecordingEnvironment>(first.Environment);
        RecordingEnvironment secondUsed = Assert.IsType<RecordingEnvironment>(second.Environment);

        Assert.NotSame(firstUsed, secondUsed);
        Assert.Equal(1, firstUsed.Resolutions);
        Assert.Equal(1, secondUsed.Resolutions);

        // The description itself took part in no run.
        Assert.Equal(0, shared.Resolutions);
    }

    [Fact]
    public async Task AnEnvironmentWithNothingPerRun_IsTheOneTheRunUses()
    {
        PlainEnvironment environment = new();

        TimelineRun run = await Timeline.Create().Trigger(new NoOpStep()).Build()
            .SetupRun()
            .SetEnv(environment)
            .RunAsync();

        run.EnsureRanToCompletion();
        Assert.Same(environment, run.Environment);
    }

    [Fact]
    public async Task ARunWithoutAnEnvironment_SaysItHadNone()
    {
        TimelineRun run = await Timeline.Create().Trigger(new NoOpStep()).Build()
            .SetupRun()
            .RunAsync();

        run.EnsureRanToCompletion();
        Assert.Null(run.Environment);
    }

    private static Task<TimelineRun> Run(IEnvironmentProvider environment)
        => Timeline.Create().Trigger(new NoOpStep()).Build()
            .SetupRun()
            .SetEnv(environment)
            .RunAsync();

    private sealed class RecordingEnvironment : EnvironmentProviderBase, IRunScopedEnvironmentFactory
    {
        public int Resolutions { get; private set; }

        public IEnvironmentProvider CreateForRun() => new RecordingEnvironment();

        public override IReadOnlyCollection<EnvComponentIdentifier> ResolveComponents(IEnumerable<ArtifactInstanceGeneric> artifacts, IEnumerable<EnvironmentRequirement> requirements)
        {
            Resolutions++;
            return base.ResolveComponents(artifacts, requirements);
        }
    }

    private sealed class PlainEnvironment : EnvironmentProviderBase
    {
    }

    private sealed class NoOpStep : Step<EmptyStepResultContext>
    {
        public override string Name => "NoOp";
        public override string Description => "NoOp";
        public override bool DoesReturn => false;

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
            => Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);

        public override Step<EmptyStepResultContext> Clone() => new NoOpStep().WithClonedOptions(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }
}
