using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        // Attempted the only way they can be attempted: through the store. Nothing can reach an
        // artifact's state or its versions directly any more, which is why these read as store calls.
        Assert.Throws<FrameworkStateException>(() => run.ArtifactStore.CaptureVersion(instance, new FrozenTestArtifactData()));
        Assert.Throws<FrameworkStateException>(() => run.ArtifactStore.MarkState(instance, ArtifactState.Cleaned));

        // A frozen artifact is frozen all the way down, or "frozen" means only "the list of versions is".
        Assert.True(instance.Reference.IsFrozen);
        Assert.Throws<FrameworkStateException>(() => run.ArtifactStore.PinReference(
            instance,
            RunContext.Ambient(new EmptyServiceProvider(), run.VariableStore, run.ArtifactStore, new ScopedLogger(null), run.Values)));
    }

    private sealed class FrozenTestArtifactDescriber : ArtifactDescriber<FrozenTestArtifactDescriber, FrozenTestArtifactData, FrozenTestArtifactReference>
    {
        public override Task Setup(RunContext context, FrozenTestArtifactData data, FrozenTestArtifactReference reference)
            => Task.CompletedTask;

        public override Task Deconstruct(RunContext context, FrozenTestArtifactReference reference)
            => Task.CompletedTask;

        public override string ToString() => "frozen-test-artifact";
    }

    private sealed class FrozenTestArtifactData : ArtifactData<FrozenTestArtifactData, FrozenTestArtifactDescriber, FrozenTestArtifactReference>
    {
        public override string ToString() => "frozen-test-data";
    }

    private sealed class FrozenTestArtifactReference : ArtifactReference<FrozenTestArtifactReference, FrozenTestArtifactDescriber, FrozenTestArtifactData>
    {
        public override Task<ArtifactResolveResult<FrozenTestArtifactDescriber, FrozenTestArtifactData, FrozenTestArtifactReference>> ResolveToDataAsync(RunContext context, ArtifactVersionIdentifier versionIdentifier)
            => Task.FromResult(new ArtifactResolveResult<FrozenTestArtifactDescriber, FrozenTestArtifactData, FrozenTestArtifactReference>
            {
                Found = true,
                Data = new FrozenTestArtifactData(),
            });

        public override void DeclareIO(Steps.Options.StepIOContract contract)
        {
        }

        public override void OnPinReference(RunContext context)
        {
        }

        public override string ToString() => "frozen-test-reference";
    }
}
