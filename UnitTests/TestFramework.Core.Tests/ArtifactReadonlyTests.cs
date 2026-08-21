using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;
using Xunit;

namespace TestFramework.Core.Tests;

/// <summary>
/// Covers <c>MarkReadonly()</c>, the only way a timeline can keep teardown from deleting an artifact.
/// </summary>
/// <remarks>
/// Deleting is the default and stays the default, so these tests pin both directions: that the opt-out
/// works, and that not using it still deletes. The opt-out also has to survive the things that used to
/// decide ownership on the user's behalf - a reference that reports itself deconstructable, and a pin
/// that flips that report mid-run.
/// </remarks>
public class ArtifactReadonlyTests
{
    [Fact]
    public async Task Default_DeletesADiscoveredArtifact()
    {
        // The baseline the opt-out is measured against. Discovery deletes unless told otherwise, and
        // that is deliberate - if this test ever flips, MarkReadonly() has become mandatory reading
        // rather than an opt-out.
        TeardownRecorder recorder = new();
        Timeline timeline = Timeline.Create()
            .FindArtifact("found", new RecordingFinder(recorder, 1))
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        run.EnsureRanToCompletion();

        Assert.Contains("found_single", recorder.Deconstructed);
    }

    [Fact]
    public async Task MarkReadonly_KeepsTeardownFromDeletingADiscoveredArtifact()
    {
        TeardownRecorder recorder = new();
        RecordingOutput output = new();
        Timeline timeline = Timeline.Create()
            .FindArtifact("found", new RecordingFinder(recorder, 1))
            .MarkReadonly()
            .Build();

        TimelineRun run = await timeline.SetupRun(null, output).RunAsync();

        run.EnsureRanToCompletion();

        Assert.Empty(recorder.Deconstructed);
        Assert.Contains("marked readonly by the timeline", output.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Could not deconstruct", output.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MarkReadonly_OutranksAReferenceThatFlipsItselfToDeconstructableWhilePinning()
    {
        // This is the shape that made ownership unreliable: the reference reports itself
        // deconstructable, and asserts that again from OnPinReference, which discovery always calls.
        // The user's choice has to win over both.
        TeardownRecorder recorder = new();
        Timeline timeline = Timeline.Create()
            .FindArtifact("found", new RecordingFinder(recorder, 1, claimsOwnershipWhenPinned: true))
            .MarkReadonly()
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        run.EnsureRanToCompletion();

        Assert.Empty(recorder.Deconstructed);
    }

    [Fact]
    public async Task MarkReadonly_ProtectsEveryArtifactOfAMultiFind()
    {
        TeardownRecorder recorder = new();
        Timeline timeline = Timeline.Create()
            .FindArtifacts("found", new RecordingFinder(recorder, 3))
            .MarkReadonly()
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        run.EnsureRanToCompletion();

        Assert.Equal(3, run.ArtifactStore.GetAll().Count());
        Assert.Empty(recorder.Deconstructed);
    }

    [Fact]
    public async Task MarkReadonly_ProtectsARegisteredArtifactToo()
    {
        TeardownRecorder recorder = new();
        Timeline timeline = Timeline.Create()
            .RegisterArtifact("registered", new RecordingReference(recorder, "registered"))
            .MarkReadonly()
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        run.EnsureRanToCompletion();

        Assert.Empty(recorder.Deconstructed);
    }

    [Fact]
    public async Task MarkReadonly_AppliesOnlyToTheStepItWasChainedOnto()
    {
        // A per-step modifier, like every other modifier. Protecting one artifact must not quietly
        // protect the next one declared after it.
        TeardownRecorder recorder = new();
        Timeline timeline = Timeline.Create()
            .RegisterArtifact("protected", new RecordingReference(recorder, "protected"))
            .MarkReadonly()
            .RegisterArtifact("owned", new RecordingReference(recorder, "owned"))
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        run.EnsureRanToCompletion();

        Assert.Contains("owned", recorder.Deconstructed);
        Assert.DoesNotContain("protected", recorder.Deconstructed);
    }

    [Fact]
    public async Task RemoveArtifact_FailsOnAReadonlyArtifactRatherThanDeletingOrSkippingIt()
    {
        TeardownRecorder recorder = new();
        Timeline timeline = Timeline.Create()
            .RegisterArtifact("protected", new RecordingReference(recorder, "protected"))
            .MarkReadonly()
            .RemoveArtifact("protected")
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        TimelineRunFailedException exception = Assert.Throws<TimelineRunFailedException>(() => run.EnsureRanToCompletion());

        Assert.Contains(exception.FailedSteps, step => step.StepException is ArtifactMarkedReadonlyException);
        Assert.Empty(recorder.Deconstructed);
    }

    [Fact]
    public async Task MarkReadonly_SurvivesASecondRunOfTheSameBuiltTimeline()
    {
        // The flag rides the step, and every run clones the step. A choice that only held for the
        // first run would be worse than no choice at all.
        TeardownRecorder recorder = new();
        Timeline timeline = Timeline.Create()
            .FindArtifact("found", new RecordingFinder(recorder, 1))
            .MarkReadonly()
            .Build();

        (await timeline.SetupRun().RunAsync()).EnsureRanToCompletion();
        (await timeline.SetupRun().RunAsync()).EnsureRanToCompletion();

        Assert.Empty(recorder.Deconstructed);
    }

    private sealed class RecordingOutput : Xunit.Abstractions.ITestOutputHelper
    {
        private readonly System.Text.StringBuilder _builder = new();

        public string Text => _builder.ToString();

        public void WriteLine(string message) => _builder.AppendLine(message);

        public void WriteLine(string format, params object[] args) => _builder.AppendLine(string.Format(format, args));
    }

    private sealed class TeardownRecorder
    {
        public ConcurrentBag<string> Deconstructed { get; } = [];
    }

    private sealed class RecordingFinder(TeardownRecorder recorder, int count, bool claimsOwnershipWhenPinned = false)
        : ArtifactFinder<RecordingDescriber, RecordingData, RecordingReference>
    {
        public override Task<ArtifactFinderResult?> FindAsync(IServiceProvider serviceProvider, VariableStore variableStore, ScopedLogger logger, CancellationToken cancellationToken)
            => Task.FromResult<ArtifactFinderResult?>(new ArtifactFinderResult(
                new RecordingReference(recorder, "found_single", claimsOwnershipWhenPinned)));

        public override Task<ArtifactFinderResultMulti> FindMultiAsync(IServiceProvider serviceProvider, VariableStore variableStore, ScopedLogger logger, CancellationToken cancellationToken)
            => Task.FromResult(new ArtifactFinderResultMulti(
                [.. Enumerable.Range(0, count).Select(i => new ArtifactFinderResult(
                    new RecordingReference(recorder, $"found_{i}", claimsOwnershipWhenPinned)))]));
    }

    private sealed class RecordingDescriber : ArtifactDescriber<RecordingDescriber, RecordingData, RecordingReference>
    {
        public override Task Setup(IServiceProvider serviceProvider, RecordingData data, RecordingReference reference, VariableStore variableStore, ScopedLogger logger)
            => Task.CompletedTask;

        public override Task Deconstruct(IServiceProvider serviceProvider, RecordingReference reference, VariableStore variableStore, ScopedLogger logger)
        {
            reference.Record();
            return Task.CompletedTask;
        }

        public override string ToString() => "recording-artifact";
    }

    private sealed class RecordingData : ArtifactData<RecordingData, RecordingDescriber, RecordingReference>
    {
        public override string ToString() => "recording-data";
    }

    private sealed class RecordingReference : ArtifactReference<RecordingReference, RecordingDescriber, RecordingData>
    {
        private readonly TeardownRecorder _recorder;
        private readonly string _name;
        private readonly bool _claimsOwnershipWhenPinned;

        public RecordingReference(TeardownRecorder recorder, string name, bool claimsOwnershipWhenPinned = false)
        {
            _recorder = recorder;
            _name = name;
            _claimsOwnershipWhenPinned = claimsOwnershipWhenPinned;
            CanDeconstruct = true;
        }

        public void Record() => _recorder.Deconstructed.Add(_name);

        public override Task<ArtifactResolveResult<RecordingDescriber, RecordingData, RecordingReference>> ResolveToDataAsync(
            IServiceProvider serviceProvider,
            ArtifactVersionIdentifier versionIdentifier,
            VariableStore variableStore,
            ScopedLogger logger)
            => Task.FromResult(new ArtifactResolveResult<RecordingDescriber, RecordingData, RecordingReference>
            {
                Found = true,
                Data = new RecordingData(),
            });

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override void OnPinReference(VariableStore variableStore, ScopedLogger logger)
        {
            if (_claimsOwnershipWhenPinned)
                CanDeconstruct = true;
        }

        public override string ToString() => $"recording-reference({_name})";
    }
}
