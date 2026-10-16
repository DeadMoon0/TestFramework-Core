using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using Xunit;

namespace TestFramework.Core.Tests;

/// <summary>
/// The public <see cref="ArtifactStore.CaptureVersion"/> boundary. Each refusal case names the
/// damage it prevents, so a request to loosen it can be weighed against its cost.
/// </summary>
public class ArtifactVersionCaptureTests
{
    [Fact]
    public void CaptureVersion_AppendsAVersion_ThroughTheFunnel()
    {
        RunContext context = RunContext.Detached();
        ArtifactInstance<CaptureTestArtifactDescriber, CaptureTestArtifactData, CaptureTestArtifactReference> instance =
            context.Artifacts.Add<CaptureTestArtifactDescriber, CaptureTestArtifactData, CaptureTestArtifactReference>(
                "versioned", new CaptureTestArtifactReference(), new CaptureTestArtifactData());

        context.Artifacts.CaptureVersion(instance, new CaptureTestArtifactData());

        Assert.Equal(2, instance.VersionCount);
    }

    [Fact]
    public void AnInstanceAnotherRunsStoreHolds_IsRefused()
    {
        // Accepted, this would version another run's artifact past that run's own licence and
        // freeze - the store doing the write judges only its own gates.
        RunContext ownerRun = RunContext.Detached();
        RunContext otherRun = RunContext.Detached();
        ArtifactInstance<CaptureTestArtifactDescriber, CaptureTestArtifactData, CaptureTestArtifactReference> instance =
            ownerRun.Artifacts.Add<CaptureTestArtifactDescriber, CaptureTestArtifactData, CaptureTestArtifactReference>(
                "owned", new CaptureTestArtifactReference(), new CaptureTestArtifactData());

        FrameworkConfigurationException refusal = Assert.Throws<FrameworkConfigurationException>(
            () => otherRun.Artifacts.CaptureVersion(instance, new CaptureTestArtifactData()));

        Assert.Contains("not held by this run's store", refusal.Message);
        Assert.Equal(1, instance.VersionCount);
    }

    [Fact]
    public async Task VersionsCapturedAtTheSameTime_AreAllKept()
    {
        // Two steps of one stage can version the same artifact at once when neither declares it, so the
        // planner has no reason to order them. Every capture is evidence; none may be lost to the race.
        const int writers = 8;
        const int capturesEach = 500;
        RunContext context = RunContext.Detached();
        ArtifactInstance<CaptureTestArtifactDescriber, CaptureTestArtifactData, CaptureTestArtifactReference> instance =
            context.Artifacts.Add<CaptureTestArtifactDescriber, CaptureTestArtifactData, CaptureTestArtifactReference>(
                "contended", new CaptureTestArtifactReference(), new CaptureTestArtifactData());

        using Barrier start = new(writers);
        Task[] running = new Task[writers];
        for (int writer = 0; writer < writers; writer++)
        {
            running[writer] = Task.Factory.StartNew(() =>
            {
                start.SignalAndWait();
                for (int capture = 0; capture < capturesEach; capture++)
                {
                    context.Artifacts.CaptureVersion(instance, new CaptureTestArtifactData());

                    // A reader during the writes must always see a whole list.
                    _ = instance.Last;
                }
            }, TaskCreationOptions.LongRunning);
        }

        await Task.WhenAll(running);

        Assert.Equal(1 + (writers * capturesEach), instance.VersionCount);
    }

    [Fact]
    public void VersionDataOfAForeignKind_IsRefused()
    {
        // Accepted, a foreign payload would surface as an InvalidCastException at whichever typed
        // version read touches it first - far from the mistake, in whoever reads the run.
        RunContext context = RunContext.Detached();
        ArtifactInstance<CaptureTestArtifactDescriber, CaptureTestArtifactData, CaptureTestArtifactReference> instance =
            context.Artifacts.Add<CaptureTestArtifactDescriber, CaptureTestArtifactData, CaptureTestArtifactReference>(
                "typed", new CaptureTestArtifactReference(), new CaptureTestArtifactData());

        FrameworkConfigurationException refusal = Assert.Throws<FrameworkConfigurationException>(
            () => context.Artifacts.CaptureVersion(instance, new OtherKindArtifactData()));

        Assert.Contains(nameof(OtherKindArtifactDescriber), refusal.Message);
        Assert.Equal(1, instance.VersionCount);
    }

    private sealed class CaptureTestArtifactData : ArtifactData<CaptureTestArtifactData, CaptureTestArtifactDescriber, CaptureTestArtifactReference>
    {
        public override string ToString() => "capture-test-data";
    }

    private sealed class CaptureTestArtifactDescriber : ArtifactDescriber<CaptureTestArtifactDescriber, CaptureTestArtifactData, CaptureTestArtifactReference>
    {
        public override Task Setup(RunContext context, CaptureTestArtifactData data, CaptureTestArtifactReference reference) => Task.CompletedTask;

        public override Task Deconstruct(RunContext context, CaptureTestArtifactReference reference) => Task.CompletedTask;

        public override string ToString() => "CaptureTest";
    }

    private sealed class CaptureTestArtifactReference : ArtifactReference<CaptureTestArtifactReference, CaptureTestArtifactDescriber, CaptureTestArtifactData>
    {
        public override Task<ArtifactResolveResult<CaptureTestArtifactDescriber, CaptureTestArtifactData, CaptureTestArtifactReference>> ResolveToDataAsync(RunContext context, ArtifactVersionIdentifier versionIdentifier)
            => Task.FromResult(new ArtifactResolveResult<CaptureTestArtifactDescriber, CaptureTestArtifactData, CaptureTestArtifactReference> { Found = false, Data = null });

        public override void DeclareIO(StepIOContract contract) { }

        public override void OnPinReference(RunContext context) { }

        public override string ToString() => "capture-test";
    }

    private sealed class OtherKindArtifactData : ArtifactData<OtherKindArtifactData, OtherKindArtifactDescriber, OtherKindArtifactReference>
    {
        public override string ToString() => "other-kind-data";
    }

    private sealed class OtherKindArtifactDescriber : ArtifactDescriber<OtherKindArtifactDescriber, OtherKindArtifactData, OtherKindArtifactReference>
    {
        public override Task Setup(RunContext context, OtherKindArtifactData data, OtherKindArtifactReference reference) => Task.CompletedTask;

        public override Task Deconstruct(RunContext context, OtherKindArtifactReference reference) => Task.CompletedTask;

        public override string ToString() => "OtherKind";
    }

    private sealed class OtherKindArtifactReference : ArtifactReference<OtherKindArtifactReference, OtherKindArtifactDescriber, OtherKindArtifactData>
    {
        public override Task<ArtifactResolveResult<OtherKindArtifactDescriber, OtherKindArtifactData, OtherKindArtifactReference>> ResolveToDataAsync(RunContext context, ArtifactVersionIdentifier versionIdentifier)
            => Task.FromResult(new ArtifactResolveResult<OtherKindArtifactDescriber, OtherKindArtifactData, OtherKindArtifactReference> { Found = false, Data = null });

        public override void DeclareIO(StepIOContract contract) { }

        public override void OnPinReference(RunContext context) { }

        public override string ToString() => "other-kind";
    }
}
