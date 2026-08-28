using TestFramework.Core.Steps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

/// <summary>
/// Covers the promise that a built timeline is a read-only template: two runs of the same timeline
/// must not see each other's state.
/// </summary>
public class ArtifactRunIsolationTests
{
    [Fact]
    public async Task RegisterArtifact_PinsItsOwnPath_WhenTheSameTimelineIsRunTwice()
    {
        // The reference resolves 'root' when it is pinned. If both runs share one reference instance,
        // the second run finds it already pinned and silently keeps the first run's path.
        PinRecorder recorder = new();
        Timeline timeline = Timeline.Create()
            .RegisterArtifact("data", new PinningArtifactReference(recorder))
            .Build();

        TimelineRun first = await timeline.SetupRun().AddVariable("root", "first").RunAsync();
        TimelineRun second = await timeline.SetupRun().AddVariable("root", "second").RunAsync();

        Assert.Equal(["first", "second"], recorder.PinnedPaths);

        ArtifactReferenceGeneric firstReference = first.ArtifactStore.GetArtifact("data").Reference;
        ArtifactReferenceGeneric secondReference = second.ArtifactStore.GetArtifact("data").Reference;

        Assert.NotSame(firstReference, secondReference);
        Assert.Equal("pinning-reference(first)", firstReference.ToString());
        Assert.Equal("pinning-reference(second)", secondReference.ToString());
    }

    [Fact]
    public void CloneForRun_ResetsPinnedAndFrozenState()
    {
        PinningArtifactReference reference = new(new PinRecorder());

        // Pinned first, then frozen: that is the order a run does it in, and since freezing a reference
        // now refuses further pinning, the old order (freeze, then pin, then assert both) only ever
        // passed because the frozen flag guarded nothing.
        ScopedLogger logger = new ScopedLogger(null);
        DebuggingRunSession session = new DebuggingRunSession(new EmptyRunDebugger());
        ArtifactStore store = new ArtifactStore(logger, session);
        VariableStore variables = new VariableStore(logger, session);

        // The reference resolves this when it pins, which is the whole point of pinning: the answer is
        // kept, so a later change to the variable cannot retarget the artifact.
        variables.SetVariable("root", "/data");

        store.PinNewReference(
            "pinned",
            reference,
            RunContext.Ambient(new EmptyServiceProvider(), variables, store, logger, ValueResolution.Empty));

        // The internal call, because the public route is gone on purpose: a reference is settled by its
        // run ending, and nothing else - not even through an interface cast - may do it.
        reference.FreezeForRunEnd();

        Assert.True(reference.IsFrozen);
        Assert.True(reference.IsPinned);

        ArtifactReferenceGeneric clone = reference.CloneForRun();

        Assert.False(clone.IsFrozen);
        Assert.False(clone.IsPinned);
        Assert.NotSame(reference, clone);
    }

    private sealed class PinRecorder
    {
        private readonly List<string> _pinnedPaths = [];

        public IReadOnlyList<string> PinnedPaths
        {
            get { lock (_pinnedPaths) { return [.. _pinnedPaths]; } }
        }

        public void Record(string path)
        {
            lock (_pinnedPaths) { _pinnedPaths.Add(path); }
        }
    }

    private sealed class PinningArtifactDescriber : ArtifactDescriber<PinningArtifactDescriber, PinningArtifactData, PinningArtifactReference>
    {
        public override Task Setup(RunContext context, PinningArtifactData data, PinningArtifactReference reference)
            => Task.CompletedTask;

        public override Task Deconstruct(RunContext context, PinningArtifactReference reference)
            => Task.CompletedTask;

        public override string ToString() => "pinning-artifact";
    }

    private sealed class PinningArtifactData : ArtifactData<PinningArtifactData, PinningArtifactDescriber, PinningArtifactReference>
    {
        public override string ToString() => "pinning-data";
    }

    private sealed class PinningArtifactReference(PinRecorder recorder) : ArtifactReference<PinningArtifactReference, PinningArtifactDescriber, PinningArtifactData>
    {
        private string _pinnedPath = "unpinned";

        public override Task<ArtifactResolveResult<PinningArtifactDescriber, PinningArtifactData, PinningArtifactReference>> ResolveToDataAsync(RunContext context, ArtifactVersionIdentifier versionIdentifier)
            => Task.FromResult(new ArtifactResolveResult<PinningArtifactDescriber, PinningArtifactData, PinningArtifactReference>
            {
                Found = true,
                Data = new PinningArtifactData(),
            });

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override void OnPinReference(RunContext context)
        {
            _pinnedPath = context.Variables?.GetVariable<string>("root") ?? "unpinned";
            recorder.Record(_pinnedPath);
        }

        public override string ToString() => $"pinning-reference({_pinnedPath})";
    }
}
