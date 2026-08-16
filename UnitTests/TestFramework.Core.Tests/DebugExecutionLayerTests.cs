using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

/// <summary>
/// Covers reporting which steps a run will execute together.
/// </summary>
/// <remarks>
/// A debugger showing a timeline as a flat list cannot answer "what ran at the same time", and a
/// consumer cannot work it out for itself: the plan depends on parallelization mode, IO access
/// conflicts and shared artifact setup resources, and a re-derivation that got any of those wrong
/// would confidently draw a plan that never ran.
/// </remarks>
public sealed class DebugExecutionLayerTests
{
    [Fact]
    public async Task IndependentStepsThatRunTogetherShareALayer()
    {
        StructureRecordingDebugger debugger = new();

        Timeline timeline = Timeline.Create()
            .Trigger(new PrepareStep())
            .Name("prepare-one")
            .Trigger(new PrepareStep())
            .Name("prepare-two")
            .Build();

        await timeline.SetupRun(new DebuggerServiceProvider(debugger)).RunAsync();

        int[] layers = LayersOf(debugger, "prepare-one", "prepare-two");

        Assert.Equal(layers[0], layers[1]);
    }

    [Fact]
    public async Task StepsThatMustBeOrderedGetSuccessiveLayers()
    {
        // Two Act steps do not merge by default, so they are a layer apart. That ordering is the
        // thing the board draws as one row after another.
        StructureRecordingDebugger debugger = new();

        Timeline timeline = Timeline.Create()
            .Trigger(new ActStep())
            .Name("act-one")
            .Trigger(new ActStep())
            .Name("act-two")
            .Build();

        await timeline.SetupRun(new DebuggerServiceProvider(debugger)).RunAsync();

        int[] layers = LayersOf(debugger, "act-one", "act-two");

        Assert.True(layers[0] < layers[1], "A step that must wait for another should be reported in a later layer.");
    }

    [Fact]
    public async Task ALayerIsReportedForEveryStepOfEveryStage()
    {
        // Including the stages the framework adds itself. A step with no layer would be drawn at the
        // top of its stage, silently claiming it ran first.
        StructureRecordingDebugger debugger = new();

        Timeline timeline = Timeline.Create()
            .Trigger(new ActStep())
            .Name("only")
            .Build();

        await timeline.SetupRun(new DebuggerServiceProvider(debugger)).RunAsync();

        TimelineRunStructure structure = debugger.Structure ?? throw new InvalidOperationException("The run reported no structure.");

        Assert.NotEmpty(structure.Stages);

        foreach (DebugStageState stage in structure.Stages)
        {
            Assert.All(stage.Steps, step => Assert.True(step.LayerIndex >= 0, $"Step '{step.Name}' in stage '{stage.Name}' has no layer."));

            if (stage.Steps.Length == 0)
                continue;

            // Layers are contiguous from zero, so a consumer can lay them out as rows without
            // leaving gaps for layers that do not exist.
            int[] distinct = [.. stage.Steps.Select(step => step.LayerIndex).Distinct().OrderBy(layer => layer)];
            Assert.Equal(Enumerable.Range(0, distinct.Length), distinct);
        }
    }

    private static int[] LayersOf(StructureRecordingDebugger debugger, params string[] labels)
    {
        TimelineRunStructure structure = debugger.Structure ?? throw new InvalidOperationException("The run reported no structure.");

        DebugStepState[] steps = [.. structure.Stages.SelectMany(stage => stage.Steps)];

        return [.. labels.Select(label => steps
            .Single(step => string.Equals(step.LabelOptions.Label, label, StringComparison.Ordinal))
            .LayerIndex)];
    }

    private sealed class DebuggerServiceProvider(IRunDebugger debugger) : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => serviceType == typeof(IRunDebugger) ? debugger : null;
    }

    private sealed class StructureRecordingDebugger : IRunDebugger
    {
        public bool IsCapturing => true;

        public TimelineRunStructure? Structure { get; private set; }

        public Task SignalInitTimelineRunAsync(string sessionId, string name, string projectPath, TimelineRunStructure runStructure, TestIdentity? identity = null)
        {
            Structure = runStructure;
            return Task.CompletedTask;
        }

        public Task SignalEntityTransitionAsync(string sessionId, DebugEntityKind entityKind, string? stage, int? stepId, DebugLifecycleState state, DebugLifecycleState? previousState = null, DebugLifecycleState? outcomeState = null, DebugFailureDetail? failure = null)
            => Task.CompletedTask;

        public Task SignalValueUpdateAsync(string sessionId, string name, DebugValueKind valueKind, string? stage, int? stepId, DebugValueEnvelope value)
            => Task.CompletedTask;

        public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry) => Task.CompletedTask;

        public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry) => Task.CompletedTask;

        public Task SignalTimelineRunFinishedAsync(string sessionId) => Task.CompletedTask;

        public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId) => Task.CompletedTask;
    }

    private class ActStep : Step<EmptyStepResultContext>
    {
        public override string Name => "act";
        public override string Description => "Does nothing.";
        public override bool DoesReturn => false;

        public override Task<EmptyStepResultContext?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
            => Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);

        public override Step<EmptyStepResultContext> Clone() => new ActStep().WithClonedOptions(this);
        public override void DeclareIO(StepIOContract contract) { }
        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
    }

    /// <summary>A step in the one phase the planner is allowed to merge.</summary>
    private sealed class PrepareStep : ActStep
    {
        public override StepExecutionPhase Phase => StepExecutionPhase.Prepare;

        public override Step<EmptyStepResultContext> Clone() => new PrepareStep().WithClonedOptions(this);
    }
}
