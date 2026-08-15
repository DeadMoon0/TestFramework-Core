using TestFramework.Core.Artifacts;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

/// <summary>
/// A completed run is handed to the test as a record of what happened. Anything that can still be
/// written to it turns an assertion helper into a way of rewriting history.
/// </summary>
public class FrozenRunTests
{
    [Fact]
    public async Task CompletedRun_RejectsFurtherVariableWrites()
    {
        TimelineRun run = await Timeline.Create().Build().SetupRun().RunAsync();

        Assert.True(run.VariableStore.IsFrozen);
        Assert.Throws<FrameworkStateException>(() => run.VariableStore.SetVariable("late", "value"));
    }

    [Fact]
    public async Task CompletedRun_FreezesTheArtifactsItHolds()
    {
        Timeline timeline = Timeline.Create()
            .RegisterArtifact("data", new FrozenTestArtifactReference())
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        ArtifactInstanceGeneric instance = run.ArtifactStore.GetArtifact("data");

        Assert.True(run.ArtifactStore.IsFrozen);
        Assert.True(instance.IsFrozen);
        Assert.Throws<FrameworkStateException>(() => instance.AddVersionGeneric(new FrozenTestArtifactData()));
        Assert.Throws<FrameworkStateException>(() => instance.State = ArtifactState.Cleaned);
    }

    private sealed class FrozenTestArtifactDescriber : ArtifactDescriber<FrozenTestArtifactDescriber, FrozenTestArtifactData, FrozenTestArtifactReference>
    {
        public override Task Setup(IServiceProvider serviceProvider, FrozenTestArtifactData data, FrozenTestArtifactReference reference, VariableStore variableStore, Logging.ScopedLogger logger)
            => Task.CompletedTask;

        public override Task Deconstruct(IServiceProvider serviceProvider, FrozenTestArtifactReference reference, VariableStore variableStore, Logging.ScopedLogger logger)
            => Task.CompletedTask;

        public override string ToString() => "frozen-test-artifact";
    }

    private sealed class FrozenTestArtifactData : ArtifactData<FrozenTestArtifactData, FrozenTestArtifactDescriber, FrozenTestArtifactReference>
    {
        public override string ToString() => "frozen-test-data";
    }

    private sealed class FrozenTestArtifactReference : ArtifactReference<FrozenTestArtifactReference, FrozenTestArtifactDescriber, FrozenTestArtifactData>
    {
        public override Task<ArtifactResolveResult<FrozenTestArtifactDescriber, FrozenTestArtifactData, FrozenTestArtifactReference>> ResolveToDataAsync(
            IServiceProvider serviceProvider,
            ArtifactVersionIdentifier versionIdentifier,
            VariableStore variableStore,
            Logging.ScopedLogger logger)
            => Task.FromResult(new ArtifactResolveResult<FrozenTestArtifactDescriber, FrozenTestArtifactData, FrozenTestArtifactReference>
            {
                Found = true,
                Data = new FrozenTestArtifactData(),
            });

        public override void DeclareIO(Steps.Options.StepIOContract contract)
        {
        }

        public override void OnPinReference(VariableStore variableStore, Logging.ScopedLogger logger)
        {
        }

        public override string ToString() => "frozen-test-reference";
    }
}
