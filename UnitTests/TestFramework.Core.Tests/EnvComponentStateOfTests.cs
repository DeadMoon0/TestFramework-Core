using TestFramework.Core.Steps.Options;
using TestFramework.Core.Artifacts;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Environment;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Steps;
using TestFramework.Core.Timelines;
using Xunit;

namespace TestFramework.Core.Tests;

/// <summary>
/// A component reads what its dependencies produced from the run that created them.
/// </summary>
/// <remarks>
/// Environments used to keep those states in a table on the environment instance, because a component had no other
/// way to reach a sibling's - and two runs sharing one instance overwrote each other's network and containers.
/// </remarks>
public class EnvComponentStateOfTests
{
    [Fact]
    public async Task AComponentReadsWhatItsDependencyProducedInThisRun()
    {
        SharedEnvironment environment = new(TimeSpan.Zero);

        TimelineRun run = await Timeline.Create().Trigger(new NoOpStep()).Build()
            .SetupRun()
            .AddArtifact("artifact", new TestArtifactReference(), new TestArtifactData())
            .SetEnv(environment)
            .RunAsync();

        run.EnsureRanToCompletion();
        Assert.True(run.EnvironmentContext.TryGetState<string>("network", out string? network));
        Assert.Equal($"server on {network}", run.EnvironmentContext.GetState<string>("server"));
    }

    [Fact]
    public async Task TwoRunsSharingOneEnvironment_EachBuildOnTheirOwnDependencies()
    {
        // The failure this replaces: the second run's server built on the first run's network. The network takes a
        // moment to create, so the two runs interleave - a table on the instance would hand one of them the other's.
        SharedEnvironment environment = new(TimeSpan.FromMilliseconds(100));

        async Task<TimelineRun> Run() => await Timeline.Create().Trigger(new NoOpStep()).Build()
            .SetupRun()
            .AddArtifact("artifact", new TestArtifactReference(), new TestArtifactData())
            .SetEnv(environment)
            .RunAsync();

        TimelineRun[] runs = await Task.WhenAll(Run(), Run());

        foreach (TimelineRun run in runs)
        {
            run.EnsureRanToCompletion();
            string network = run.EnvironmentContext.GetState<string>("network")!;
            Assert.Equal($"server on {network}", run.EnvironmentContext.GetState<string>("server"));
        }

        Assert.NotEqual(runs[0].EnvironmentContext.GetState<string>("network"), runs[1].EnvironmentContext.GetState<string>("network"));
    }

    [Fact]
    public async Task ReadingAComponentThatIsNotADeclaredDependency_IsRefusedByName()
    {
        // Only a declared dependency is certain to exist first; anything else relies on an order nothing promised.
        SharedEnvironment environment = new(TimeSpan.Zero, serverReadsUndeclared: true);

        TimelineRun run = await Timeline.Create().Trigger(new NoOpStep()).Build()
            .SetupRun()
            .AddArtifact("artifact", new TestArtifactReference(), new TestArtifactData())
            .SetEnv(environment)
            .RunAsync();

        TimelineRunFailedException failure = Assert.Throws<TimelineRunFailedException>(run.EnsureRanToCompletion);
        Assert.Contains("reads the state of 'volume', which it does not declare as a dependency", failure.ToString(), StringComparison.Ordinal);
    }

    private sealed class SharedEnvironment : EnvironmentProviderBase
    {
        public SharedEnvironment(TimeSpan networkDelay, bool serverReadsUndeclared = false)
        {
            AddComponent(new NetworkComponent(networkDelay));
            AddComponent(new ServerComponent(serverReadsUndeclared ? "volume" : "network"));
            MapArtifact<TestArtifactDescriber>("server");
        }
    }

    private sealed class NetworkComponent(TimeSpan delay) : EnvComponent
    {
        private static int created;

        public override EnvComponentIdentifier Id => "network";

        public override async Task<object?> CreateAsync(IEnvironmentProvider environment, RunContext context)
        {
            int number = Interlocked.Increment(ref created);
            await Task.Delay(delay);
            return $"net-{number}";
        }

        public override Task DeconstructAsync(object? state, IEnvironmentProvider environment, RunContext context) => Task.CompletedTask;
    }

    private sealed class ServerComponent(string reads) : EnvComponent
    {
        public override EnvComponentIdentifier Id => "server";

        public override IReadOnlyList<EnvComponentIdentifier> Dependencies => ["network"];

        public override Task<object?> CreateAsync(IEnvironmentProvider environment, RunContext context)
            => Task.FromResult<object?>($"server on {StateOf<string>(context, reads)}");

        public override Task DeconstructAsync(object? state, IEnvironmentProvider environment, RunContext context) => Task.CompletedTask;
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

    private sealed class TestArtifactDescriber : ArtifactDescriber<TestArtifactDescriber, TestArtifactData, TestArtifactReference>
    {
        public override Task Setup(RunContext context, TestArtifactData data, TestArtifactReference reference)
            => Task.CompletedTask;

        public override Task Deconstruct(RunContext context, TestArtifactReference reference)
            => Task.CompletedTask;

        public override string ToString() => nameof(TestArtifactDescriber);
    }

    private sealed class TestArtifactData : ArtifactData<TestArtifactData, TestArtifactDescriber, TestArtifactReference>
    {
        public override string ToString() => nameof(TestArtifactData);
    }

    private sealed class TestArtifactReference : ArtifactReference<TestArtifactReference, TestArtifactDescriber, TestArtifactData>
    {
        public override Task<ArtifactResolveResult<TestArtifactDescriber, TestArtifactData, TestArtifactReference>> ResolveToDataAsync(RunContext context, ArtifactVersionIdentifier versionIdentifier)
            => Task.FromResult(new ArtifactResolveResult<TestArtifactDescriber, TestArtifactData, TestArtifactReference>
            {
                Found = true,
                Data = new TestArtifactData(),
            });

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override void OnPinReference(RunContext context)
        {
        }

        public override string ToString() => nameof(TestArtifactReference);
    }
}
