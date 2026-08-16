using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

/// <summary>
/// Covers artifact updates reaching a debugger when the instance is mutated in place.
/// </summary>
/// <remarks>
/// <c>CaptureArtifactVersion</c> exists so a run can show a value evolving — register once, capture
/// again after each acting step, compare. Every one of those mutations happens on the instance the
/// store already holds, so none of them passed through <c>AddArtifact</c>, the only thing that
/// published. A debugger therefore saw version one and the initial state, and nothing after: the
/// feature was invisible to the only consumer built to display it.
/// </remarks>
public sealed class ArtifactVersionPublicationTests
{
    [Fact]
    public async Task EachCapturedVersionIsPublished()
    {
        ValueRecordingDebugger debugger = new();

        Timeline timeline = Timeline.Create()
            .RegisterArtifact("tracked", new CountingArtifactReference())
            .CaptureArtifactVersion("tracked", "v2")
            .CaptureArtifactVersion("tracked", "v3")
            .Build();

        await timeline.SetupRun(new DebuggerServiceProvider(debugger)).RunAsync();

        IReadOnlyList<DebugValueEnvelope> updates = debugger.ArtifactUpdatesFor("tracked");

        // Registration plus two captures, and each capture must be visible as its own update.
        Assert.True(updates.Count >= 3, $"Expected at least three artifact updates, saw {updates.Count}.");

        int[] versionCounts = [.. updates.Select(u => (int)u.Core!["versionCount"]!)];
        Assert.Contains(2, versionCounts);
        Assert.Contains(3, versionCounts);
    }

    [Fact]
    public async Task ThePublishedPayloadCarriesTheWholeVersionHistory()
    {
        // The artifact rail draws v1 -> v2 -> v3 from this, so one update has to describe the whole
        // journey rather than only the newest entry.
        ValueRecordingDebugger debugger = new();

        Timeline timeline = Timeline.Create()
            .RegisterArtifact("tracked", new CountingArtifactReference())
            .CaptureArtifactVersion("tracked", "v2")
            .CaptureArtifactVersion("tracked", "v3")
            .Build();

        await timeline.SetupRun(new DebuggerServiceProvider(debugger)).RunAsync();

        DebugValueEnvelope latest = debugger.ArtifactUpdatesFor("tracked")
            .OrderByDescending(u => (int)u.Core!["versionCount"]!)
            .First();

        string[] versions = [.. ((JArray)latest.Core!["versions"]!).Select(v => (string)v!)];

        Assert.Equal(3, versions.Length);
        Assert.Equal("v2", versions[1]);
        Assert.Equal("v3", versions[2]);
        Assert.Equal(2, (int)latest.Core["versionIndex"]!);
    }

    [Fact]
    public async Task LifecycleStateChangesArePublished()
    {
        // Setup and teardown also mutate in place, so without an explicit publication the UI would
        // show every artifact as NotSetup for the whole run.
        ValueRecordingDebugger debugger = new();

        Timeline timeline = Timeline.Create()
            .RegisterArtifact("tracked", new CountingArtifactReference())
            .SetupArtifact("tracked")
            .Build();

        await timeline.SetupRun(new DebuggerServiceProvider(debugger)).RunAsync();

        string[] states = [.. debugger.ArtifactUpdatesFor("tracked").Select(u => (string)u.Core!["state"]!)];

        Assert.Contains(nameof(TestFramework.Core.Artifacts.ArtifactState.Setup), states);
        Assert.Contains(nameof(TestFramework.Core.Artifacts.ArtifactState.Cleaned), states);
    }

    private sealed class DebuggerServiceProvider(IRunDebugger debugger) : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => serviceType == typeof(IRunDebugger) ? debugger : null;
    }

    private sealed class ValueRecordingDebugger : IRunDebugger
    {
        private readonly List<(string Name, DebugValueEnvelope Envelope)> updates = [];

        public bool IsCapturing => true;

        public IReadOnlyList<DebugValueEnvelope> ArtifactUpdatesFor(string name)
        {
            lock (updates)
            {
                return [.. updates.Where(u => u.Name == name).Select(u => u.Envelope)];
            }
        }

        public Task SignalValueUpdateAsync(string sessionId, string name, DebugValueKind valueKind, string? stage, int? stepId, DebugValueEnvelope value)
        {
            if (valueKind == DebugValueKind.Artifact)
            {
                lock (updates) updates.Add((name, value));
            }

            return Task.CompletedTask;
        }

        public Task SignalInitTimelineRunAsync(string sessionId, string name, string projectPath, TimelineRunStructure runStructure, TestIdentity? identity = null) => Task.CompletedTask;
        public Task SignalEntityTransitionAsync(string sessionId, DebugEntityKind entityKind, string? stage, int? stepId, DebugLifecycleState state, DebugLifecycleState? previousState = null, DebugLifecycleState? outcomeState = null, DebugFailureDetail? failure = null) => Task.CompletedTask;
        public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry) => Task.CompletedTask;
        public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry) => Task.CompletedTask;
        public Task SignalTimelineRunFinishedAsync(string sessionId) => Task.CompletedTask;
        public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId) => Task.CompletedTask;
    }

    private sealed class CountingArtifactData : ArtifactData<CountingArtifactData, CountingArtifactDescriber, CountingArtifactReference>
    {
        public override string ToString() => "counting-data";
    }

    private sealed class CountingArtifactDescriber : ArtifactDescriber<CountingArtifactDescriber, CountingArtifactData, CountingArtifactReference>
    {
        public override Task Setup(IServiceProvider serviceProvider, CountingArtifactData data, CountingArtifactReference reference, VariableStore variableStore, ScopedLogger logger)
            => Task.CompletedTask;

        public override Task Deconstruct(IServiceProvider serviceProvider, CountingArtifactReference reference, VariableStore variableStore, ScopedLogger logger)
            => Task.CompletedTask;

        public override string ToString() => "counting-artifact";
    }

    private sealed class CountingArtifactReference : ArtifactReference<CountingArtifactReference, CountingArtifactDescriber, CountingArtifactData>
    {
        public CountingArtifactReference() => CanDeconstruct = true;

        public override Task<ArtifactResolveResult<CountingArtifactDescriber, CountingArtifactData, CountingArtifactReference>> ResolveToDataAsync(
            IServiceProvider serviceProvider,
            ArtifactVersionIdentifier versionIdentifier,
            VariableStore variableStore,
            ScopedLogger logger)
            => Task.FromResult(new ArtifactResolveResult<CountingArtifactDescriber, CountingArtifactData, CountingArtifactReference>
            {
                Found = true,
                Data = new CountingArtifactData { Identifier = versionIdentifier }
            });

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override void OnPinReference(VariableStore variableStore, ScopedLogger logger)
        {
        }

        public override string ToString() => "counting-reference";
    }
}
