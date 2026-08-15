using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

/// <summary>
/// Covers what teardown does with artifacts it cannot or should not remove.
/// </summary>
/// <remarks>
/// Teardown runs after the interesting part of a test is over, so a defect here is invisible until
/// a later run trips over data that was supposed to be gone.
/// </remarks>
public class ArtifactTeardownTests
{
    [Fact]
    public async Task DeconstructAll_RemovesAnOwnedArtifactDeclaredAfterOneThatWasNeverFound()
    {
        // Both are registered in the main stage, so they reach the store in this order. 'missing'
        // resolves to nothing and never reaches the Setup state; teardown has to carry on past it
        // rather than stop, or everything declared later is silently left behind.
        TeardownRecorder recorder = new();
        Timeline timeline = Timeline.Create()
            .RegisterArtifact("missing", new RecordingArtifactReference(recorder, "missing", found: false, canDeconstruct: true))
            .RegisterArtifact("adopted", new RecordingArtifactReference(recorder, "adopted", found: true, canDeconstruct: true))
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        run.EnsureRanToCompletion();

        Assert.Contains("adopted", recorder.Deconstructed);
        Assert.DoesNotContain("missing", recorder.Deconstructed);
    }

    [Fact]
    public async Task DeconstructAll_LeavesAnObservedArtifactAloneWithoutReportingAFailure()
    {
        // An artifact a finder produced cannot be deconstructed by design. Passing over it is the
        // expected outcome, so the run log must not carry an error a reader has to learn to ignore.
        TeardownRecorder recorder = new();
        RecordingOutput output = new();
        Timeline timeline = Timeline.Create()
            .RegisterArtifact("observed", new RecordingArtifactReference(recorder, "observed", found: true, canDeconstruct: false))
            .Build();

        TimelineRun run = await timeline.SetupRun(null, output).RunAsync();

        run.EnsureRanToCompletion();

        Assert.DoesNotContain("observed", recorder.Deconstructed);
        Assert.DoesNotContain("Could not deconstruct", output.Text, StringComparison.Ordinal);
        Assert.Contains("observed rather than owned", output.Text, StringComparison.Ordinal);
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

    private sealed class RecordingArtifactDescriber : ArtifactDescriber<RecordingArtifactDescriber, RecordingArtifactData, RecordingArtifactReference>
    {
        public override Task Setup(IServiceProvider serviceProvider, RecordingArtifactData data, RecordingArtifactReference reference, VariableStore variableStore, ScopedLogger logger)
            => Task.CompletedTask;

        public override Task Deconstruct(IServiceProvider serviceProvider, RecordingArtifactReference reference, VariableStore variableStore, ScopedLogger logger)
        {
            reference.Record();
            return Task.CompletedTask;
        }

        public override string ToString() => "recording-artifact";
    }

    private sealed class RecordingArtifactData : ArtifactData<RecordingArtifactData, RecordingArtifactDescriber, RecordingArtifactReference>
    {
        public override string ToString() => "recording-data";
    }

    private sealed class RecordingArtifactReference : ArtifactReference<RecordingArtifactReference, RecordingArtifactDescriber, RecordingArtifactData>
    {
        private readonly TeardownRecorder _recorder;
        private readonly string _name;
        private readonly bool _found;

        public RecordingArtifactReference(TeardownRecorder recorder, string name, bool found, bool canDeconstruct)
        {
            _recorder = recorder;
            _name = name;
            _found = found;
            CanDeconstruct = canDeconstruct;
        }

        public void Record() => _recorder.Deconstructed.Add(_name);

        public override Task<ArtifactResolveResult<RecordingArtifactDescriber, RecordingArtifactData, RecordingArtifactReference>> ResolveToDataAsync(
            IServiceProvider serviceProvider,
            ArtifactVersionIdentifier versionIdentifier,
            VariableStore variableStore,
            ScopedLogger logger)
            => Task.FromResult(new ArtifactResolveResult<RecordingArtifactDescriber, RecordingArtifactData, RecordingArtifactReference>
            {
                Found = _found,
                Data = _found ? new RecordingArtifactData() : null,
            });

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override void OnPinReference(VariableStore variableStore, ScopedLogger logger)
        {
        }

        public override string ToString() => $"recording-reference({_name})";
    }
}
