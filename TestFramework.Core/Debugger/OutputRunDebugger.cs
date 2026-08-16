using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using Xunit.Abstractions;

namespace TestFramework.Core.Debugger;

internal sealed class OutputRunDebugger : IRunDebugger
{
    private const string DisableUnicodeOutEnvironmentVariable = "TestFramework_Disable_Unicode_Out";
    private const int PanelWidth = 95;
    private const int PanelInnerWidth = PanelWidth - 4;
    /// <summary>
    /// Steps in one layer run concurrently, so their signals arrive on several threads at once. Every
    /// handler below reads and writes the shared render state, several of them across more than one
    /// container, so the whole handler body has to be one critical section — a concurrent collection
    /// per container would not make those read-modify-write pairs atomic.
    /// </summary>
    private readonly object renderGate = new();

    private readonly LogLineWriter writer;
    private readonly bool useAsciiOutput;
    private TimelineRunStructure? runStructure;
    private readonly Dictionary<string, int> stepIterations = [];
    private readonly List<StageRenderState> orderedStages = [];
    private readonly Dictionary<string, StageRenderState> stagesByName = new(System.StringComparer.Ordinal);
    private readonly Dictionary<string, VariableState> variablesByKey = new(System.StringComparer.Ordinal);
    private readonly Dictionary<string, ArtifactState> artifactsByKey = new(System.StringComparer.Ordinal);
    private readonly List<string> runLogLines = [];
    private readonly List<string> assertionLines = [];
    private string? runName;
    private string? projectPath;

    public OutputRunDebugger(ITestOutputHelper outputHelper)
    {
        writer = new LogLineWriter(outputHelper, "\t");
        useAsciiOutput = IsUnicodeOutputDisabled();
    }

    public Task SignalInitTimelineRunAsync(string sessionId, string name, string projectPath, TimelineRunStructure runStructure, TestIdentity? identity = null)
    {
        lock (renderGate)
        {
            HandleInitTimelineRun(name, projectPath, runStructure);
        }

        return Task.CompletedTask;
    }

    private void HandleInitTimelineRun(string name, string projectPath, TimelineRunStructure runStructure)
    {
        runName = name;
        this.projectPath = projectPath;
        this.runStructure = runStructure;
        stepIterations.Clear();
        orderedStages.Clear();
        stagesByName.Clear();
        variablesByKey.Clear();
        artifactsByKey.Clear();
        runLogLines.Clear();
        assertionLines.Clear();

        foreach (VariableState variable in runStructure.Variables.Values)
            variablesByKey[variable.Key] = variable;

        foreach (ArtifactState artifact in runStructure.Artifacts.Values)
            artifactsByKey[artifact.Key] = artifact;
    }

    public Task SignalEntityTransitionAsync(string sessionId, DebugEntityKind entityKind, string? stage, int? stepId, DebugLifecycleState state, DebugLifecycleState? previousState = null, DebugLifecycleState? outcomeState = null, DebugFailureDetail? failure = null)
    {
        lock (renderGate)
        {
            HandleEntityTransition(entityKind, stage, stepId, state, previousState, outcomeState);
        }

        return Task.CompletedTask;
    }

    private void HandleEntityTransition(DebugEntityKind entityKind, string? stage, int? stepId, DebugLifecycleState state, DebugLifecycleState? previousState, DebugLifecycleState? outcomeState)
    {
        if (entityKind == DebugEntityKind.Stage && stage is not null && state == DebugLifecycleState.Running)
        {
            EnsureStage(stage);
            return;
        }

        if (entityKind != DebugEntityKind.Step || stage is null || stepId is null)
            return;

        StageRenderState stageState = EnsureStage(stage);
        string stepKey = GetStepKey(stage, stepId.Value);
        DebugStepState? step = FindStepDefinition(stage, stepId.Value);
        string stepDisplayName = GetStepDisplayName(step);

        if (state == DebugLifecycleState.Running)
        {
            int iteration = previousState == DebugLifecycleState.WaitingForRetry && stepIterations.TryGetValue(stepKey, out int currentIteration)
                ? currentIteration + 1
                : 1;

            stepIterations[stepKey] = iteration;
            StepRenderState stepRun = stageState.StartStep(stepId.Value, iteration, step, stepDisplayName);
            stageState.FlowEvents.Add(new FlowEventRenderState(
                iteration == 1 ? "RUN " : "RETRY",
                stepRun.Marker,
                iteration == 1 ? $"-> {stepDisplayName}" : $"-> {stepDisplayName} (retry {iteration})",
                stepId.Value,
                true));
            return;
        }

        if (previousState == DebugLifecycleState.Running)
        {
            DebugLifecycleState effectiveState = outcomeState ?? state;
            int iteration = stepIterations.TryGetValue(stepKey, out int currentIterationValue) ? currentIterationValue : 1;
            StepRenderState stepRun = stageState.GetOrCreateStep(stepId.Value, iteration, step, stepDisplayName);
            stepRun.State = effectiveState;
            stepRun.Completed = true;
            stageState.FlowEvents.Add(new FlowEventRenderState(
                MapStateBadge(effectiveState),
                stepRun.Marker,
                effectiveState == DebugLifecycleState.WaitingForRetry
                    ? $"<- {stepDisplayName} waiting for retry"
                    : $"<- {stepDisplayName}",
                stepId.Value,
                false));
        }
    }

    public Task SignalValueUpdateAsync(string sessionId, string name, DebugValueKind valueKind, string? stage, int? stepId, DebugValueEnvelope value)
    {
        lock (renderGate)
        {
            HandleValueUpdate(name, valueKind, stage, stepId, value);
        }

        return Task.CompletedTask;
    }

    private void HandleValueUpdate(string name, DebugValueKind valueKind, string? stage, int? stepId, DebugValueEnvelope value)
    {
        switch (valueKind)
        {
            case DebugValueKind.Variable:
                variablesByKey[name] = new VariableState { Key = name, Envelope = value };
                break;
            case DebugValueKind.Artifact:
                artifactsByKey[name] = new ArtifactState { Key = name, Envelope = value };
                break;
        }

        if (stage is null || stepId is null)
            return;

        StepRenderState? stepRun = FindActiveStep(stage, stepId.Value);
        if (stepRun is null)
            return;

        stepRun.ValueUpdates.Add(new ValueUpdateRenderState(name, valueKind, value.DisplayText));
    }

    public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry)
    {
        lock (renderGate)
        {
            HandleLogEntry(entry);
        }

        return Task.CompletedTask;
    }

    private void HandleLogEntry(DebugLogEntry entry)
    {
        string[] lines = entry.Lines.Length == 0
            ? (entry.Message.Length == 0 ? [] : entry.Message.Split(["\r\n", "\r", "\n", "\n\r"], System.StringSplitOptions.None))
            : entry.Lines;

        if (entry.Stage is null)
        {
            foreach (string line in lines)
                runLogLines.Add(string.IsNullOrWhiteSpace(line) ? string.Empty : line);

            return;
        }

        if (entry.StepId is null || entry.Iteration is null)
        {
            StageRenderState stage = EnsureStage(entry.Stage);
            foreach (string line in lines)
                stage.LogLines.Add(string.IsNullOrWhiteSpace(line) ? string.Empty : line);

            return;
        }

        string stepKey = GetStepKey(entry.Stage, entry.StepId.Value);
        if (!stepIterations.TryGetValue(stepKey, out int activeIteration) || activeIteration != entry.Iteration.Value)
            return;

        StepRenderState? stepRun = FindActiveStep(entry.Stage, entry.StepId.Value, entry.Iteration.Value);
        if (stepRun is null)
            return;

        foreach (string line in lines)
            stepRun.LogLines.Add(string.IsNullOrWhiteSpace(line) ? string.Empty : line);
    }

    public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry)
    {
        lock (renderGate)
        {
            HandleAssertion(entry);
        }

        return Task.CompletedTask;
    }

    private void HandleAssertion(DebugAssertionEntry entry)
    {
        assertionLines.Add(entry.Succeeded
            ? $"[PASS] {entry.Target}  {entry.AssertionDisplay}"
            : $"[FAIL] {entry.Target}  {entry.AssertionDisplay}");

        if (!entry.Succeeded && !string.IsNullOrWhiteSpace(entry.FailureReason))
            assertionLines.Add($"       {entry.FailureReason}");
    }

    public Task SignalTimelineRunFinishedAsync(string sessionId)
    {
        // Rendering walks every container the handlers write to, so it belongs inside the same gate.
        lock (renderGate)
        {
            RenderRunSummary();
        }

        return Task.CompletedTask;
    }

    public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId)
    {
        return Task.CompletedTask;
    }

    private void RenderRunSummary()
    {
        BoxPrefix runPrefix = CreateRootBoxPrefix();
        WriteTopBorder('=', runPrefix);
        WriteWrappedContent($"TIMELINE DEBUG VIEW  {runName ?? "<unnamed>"}", runPrefix, false);
        if (!string.IsNullOrWhiteSpace(projectPath))
            WriteWrappedContent($"Project: {projectPath}", runPrefix, false);
        WriteBottomBorder('=', runPrefix);

        if (runLogLines.Count > 0)
        {
            BoxPrefix runLogPrefix = CreateBoxPrefix(string.Empty, orderedStages.Count > 0 || assertionLines.Count > 0);
            WriteGapLine(runLogPrefix.Gap);
            RenderSection("Run Log", runLogLines, runLogPrefix);
        }

        for (int stageIndex = 0; stageIndex < orderedStages.Count; stageIndex++)
        {
            bool hasFollowingSibling = stageIndex < orderedStages.Count - 1 || assertionLines.Count > 0;
            BoxPrefix stagePrefix = CreateBoxPrefix(string.Empty, hasFollowingSibling);
            WriteGapLine(stagePrefix.Gap);
            RenderStage(orderedStages[stageIndex], stagePrefix);
        }

        if (assertionLines.Count > 0)
        {
            BoxPrefix assertionPrefix = CreateBoxPrefix(string.Empty, false);
            WriteGapLine(assertionPrefix.Gap);
            WriteSectionHeader("Assertions", assertionPrefix);

            bool connectToBox = true;
            foreach (string assertionLine in assertionLines)
            {
                WriteWrappedContent(assertionLine, assertionPrefix, connectToBox);
                connectToBox = false;
            }

            WriteBottomBorder('-', assertionPrefix);
        }
    }

    private void RenderStage(StageRenderState stage, BoxPrefix stagePrefix)
    {
        int layerCount = stage.LayerPlans.Count;
        int peakParallel = stage.LayerPlans.Count == 0 ? 0 : stage.LayerPlans.Max(candidate => candidate.StepIds.Length);
        IReadOnlyList<StepGroupRenderState> stepGroups = stage.GetOrderedStepGroups();
        WriteTopBorder('=', stagePrefix);
        WriteKeyValueContent(
            $"STAGE  {stage.Name}",
            $"steps: {stage.StepCount} | layers: {layerCount} | peak parallel: {peakParallel}",
            stagePrefix,
            true);
        if (!string.IsNullOrWhiteSpace(stage.Description))
            WriteWrappedContent(stage.Description, stagePrefix, false);
        WriteBottomBorder('=', stagePrefix);

        if (stage.LogLines.Count > 0)
        {
            WriteGapLine(stagePrefix.Rest);
            RenderSection("Stage Activity", stage.LogLines, stagePrefix);
        }

        string childAncestorPrefix = stagePrefix.Rest;
        int childCount = 1 + stepGroups.Count;
        BoxPrefix flowTracePrefix = CreateBoxPrefix(childAncestorPrefix, childCount > 1);
        WriteGapLine(flowTracePrefix.Gap);
        RenderFlowTrace(stage, flowTracePrefix);

        for (int stepIndex = 0; stepIndex < stepGroups.Count; stepIndex++)
        {
            bool hasFollowingSibling = stepIndex < stepGroups.Count - 1;
            BoxPrefix stepPrefix = CreateBoxPrefix(childAncestorPrefix, hasFollowingSibling);
            WriteGapLine(stepPrefix.Gap);
            RenderStep(stepGroups[stepIndex], stepPrefix);
        }
    }

    private void RenderStep(StepGroupRenderState stepGroup, BoxPrefix stepPrefix)
    {
        StepRenderState firstAttempt = stepGroup.FirstAttempt;
        StepRenderState lastAttempt = stepGroup.LastAttempt;
        string[] inputLines = RenderInputLines(firstAttempt);
        string[] outputLines = RenderOutputLines(lastAttempt);
        string title = $"Step {stepGroup.Marker}  {stepGroup.DisplayName}";
        WriteTitledBorder(title, '-', stepPrefix);
        WriteKeyValueContent($"Phase: {stepGroup.PhaseDisplay}", GetStepMetadata(stepGroup), stepPrefix, true);
        if (!string.IsNullOrWhiteSpace(firstAttempt.Definition?.Description))
            WriteWrappedContent($"Summary: {firstAttempt.Definition.Description}", stepPrefix, false);

        if (ShouldRenderSideBySide(inputLines, outputLines))
            RenderDualBoxSection("Inputs", inputLines, "Outputs", outputLines, stepPrefix);
        else
        {
            RenderBoxSection("Inputs", inputLines, stepPrefix);
            RenderBoxSection("Outputs", outputLines, stepPrefix);
        }

        foreach (StepRenderState attempt in stepGroup.Attempts)
            RenderBoxSection($"Logs Attempt {attempt.Iteration}", attempt.LogLines.Count == 0 ? ["(no log lines)"] : attempt.LogLines, stepPrefix);

        RenderBoxSection("Final Result", BuildFinalResultLines(stepGroup), stepPrefix);
        WriteBottomBorder('-', stepPrefix);
    }

    private void RenderSection(string title, IReadOnlyCollection<string> lines, BoxPrefix prefix)
    {
        WriteSectionHeader(title, prefix);

        bool connectToBox = true;
        foreach (string line in lines)
        {
            WriteWrappedContent(line, prefix, connectToBox);
            connectToBox = false;
        }

        WriteBottomBorder('-', prefix);
    }

    private void RenderFlowTrace(StageRenderState stage, BoxPrefix prefix)
    {
        WriteSectionHeader("Flow Trace", prefix);

        if (stage.FlowEvents.Count == 0)
        {
            WriteWrappedContent("(no events captured)", prefix, true);
            WriteBottomBorder('-', prefix);
            return;
        }

        bool connectToBox = true;
        HashSet<int> emittedLayers = [];

        for (int index = 0; index < stage.FlowEvents.Count; index++)
        {
            FlowEventRenderState entry = stage.FlowEvents[index];

            if (entry.StartsExecution
                && entry.StepId is int stepId
                && stage.TryGetLayerIndex(stepId, out int layerIndex)
                && emittedLayers.Add(layerIndex)
                && stage.TryCreateLayerBanner(layerIndex, out string? banner))
            {
                WriteWrappedContent(banner!, prefix, connectToBox);
                connectToBox = false;
            }

            WriteWrappedContent($"{index + 1,2}. [{entry.Badge}] {entry.Marker,-8} {entry.Message}", prefix, connectToBox);
            connectToBox = false;
        }

        WriteBottomBorder('-', prefix);
    }

    private static string GetStepMetadata(StepGroupRenderState stepGroup)
    {
        string attemptsText = stepGroup.Attempts.Count == 1 ? "1 attempt" : $"{stepGroup.Attempts.Count} attempts";
        return stepGroup.LayerDisplay is null
            ? attemptsText
            : $"{attemptsText} | Layer: {stepGroup.LayerDisplay}";
    }

    private static string[] BuildFinalResultLines(StepGroupRenderState stepGroup)
    {
        return
        [
            $"State: {MapStateLabel(stepGroup.LastAttempt.State)}",
            $"Attempts: {stepGroup.Attempts.Count}"
        ];
    }

    private void RenderBoxSection(string title, IReadOnlyCollection<string> lines, BoxPrefix prefix)
    {
        WriteSeparator(prefix);
        WriteWrappedContent(title.ToUpperInvariant(), prefix, false);
        foreach (string line in lines)
            WriteWrappedContent($"- {line}", prefix, false);
    }

    private void RenderDualBoxSection(string leftTitle, IReadOnlyCollection<string> leftLines, string rightTitle, IReadOnlyCollection<string> rightLines, BoxPrefix prefix)
    {
        int columnWidth = (PanelInnerWidth - 3) / 2;
        string[] wrappedLeft = FlattenForColumn(leftTitle, leftLines, columnWidth);
        string[] wrappedRight = FlattenForColumn(rightTitle, rightLines, columnWidth);
        int rowCount = Math.Max(wrappedLeft.Length, wrappedRight.Length);

        WriteSeparator(prefix);
        for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            string left = rowIndex < wrappedLeft.Length ? wrappedLeft[rowIndex] : string.Empty;
            string right = rowIndex < wrappedRight.Length ? wrappedRight[rowIndex] : string.Empty;
            writer.WriteLine($"{prefix.Rest}{OuterVertical} {left.PadRight(columnWidth)} {InnerVertical} {right.PadRight(columnWidth)} {OuterVertical}");
        }
    }

    private string[] RenderInputLines(StepRenderState stepRun)
    {
        if (stepRun.InputSnapshots.Count > 0)
            return [.. stepRun.InputSnapshots.Select(RenderInputSnapshotLine)];

        StepIOEntry[] inputs = stepRun.Definition?.IOContract.Inputs.ToArray() ?? [];
        if (inputs.Length == 0)
            return ["(none declared)"];

        return [.. inputs.Select(RenderInputLine)];
    }

    private string RenderInputLine(StepIOEntry entry)
    {
        string requirement = entry.Required ? "required" : "optional";
        string kindLabel = entry.Kind == StepIOKind.Variable ? "Variable" : "Artifact";
        string? value = entry.Kind == StepIOKind.Variable
            ? variablesByKey.GetValueOrDefault(entry.Key)?.Envelope.DisplayText
            : artifactsByKey.GetValueOrDefault(entry.Key)?.Envelope.DisplayText;

        return value is null
            ? $"{kindLabel} {entry.Key}  [{requirement}]  <not available>"
            : $"{kindLabel} {entry.Key}  [{requirement}]  = {value}";
    }

    private static string RenderInputSnapshotLine(InputSnapshotRenderState entry)
    {
        string requirement = entry.Required ? "required" : "optional";
        string kindLabel = entry.Kind == StepIOKind.Variable ? "Variable" : "Artifact";

        return entry.DisplayText is null
            ? $"{kindLabel} {entry.Key}  [{requirement}]  <not available>"
            : $"{kindLabel} {entry.Key}  [{requirement}]  = {entry.DisplayText}";
    }

    private string[] RenderOutputLines(StepRenderState stepRun)
    {
        List<string> lines = [];
        List<ValueUpdateRenderState> remainingUpdates = [.. stepRun.ValueUpdates];
        StepIOEntry[] declaredOutputs = stepRun.Definition?.IOContract.Outputs.ToArray() ?? [];

        foreach (StepIOEntry output in declaredOutputs)
        {
            ValueUpdateRenderState? match = remainingUpdates.FirstOrDefault(candidate => candidate.Name == output.Key && candidate.ValueKind == MapValueKind(output.Kind));
            if (match is null)
            {
                lines.Add($"{GetValueKindLabel(MapValueKind(output.Kind))} {output.Key}  [missing]");
                continue;
            }

            lines.Add($"{GetValueKindLabel(match.ValueKind)} {match.Name}  [observed]  = {match.DisplayText}");
            remainingUpdates.Remove(match);
        }

        foreach (ValueUpdateRenderState update in remainingUpdates)
            lines.Add($"{GetValueKindLabel(update.ValueKind)} {update.Name}  [observed]  = {update.DisplayText}");

        return lines.Count == 0 ? ["(none observed)"] : [.. lines];
    }

    private StageRenderState EnsureStage(string stage)
    {
        if (stagesByName.TryGetValue(stage, out StageRenderState? existing))
            return existing;

        DebugStageState? definition = runStructure?.Stages.FirstOrDefault(candidate => candidate.Name == stage);
        StageRenderState created = new(stage, definition, variablesByKey, artifactsByKey);
        stagesByName.Add(stage, created);
        orderedStages.Add(created);
        return created;
    }

    private static string GetStepKey(string stage, int stepId) => $"{stage}:{stepId}";

    private StepRenderState? FindActiveStep(string stage, int stepId, int? iteration = null)
    {
        if (!stagesByName.TryGetValue(stage, out StageRenderState? stageState))
            return null;

        int effectiveIteration = iteration ?? GetActiveIteration(stage, stepId);
        return stageState.TryGetStep(stepId, effectiveIteration);
    }

    private DebugStepState? FindStepDefinition(string stage, int stepId)
    {
        DebugStageState? stageState = runStructure?.Stages.FirstOrDefault(candidate => candidate.Name == stage);
        return stageState is null || stepId < 0 || stepId >= stageState.Steps.Length
            ? null
            : stageState.Steps[stepId];
    }

    private static string GetStepDisplayName(DebugStepState? step)
    {
        if (step is null)
            return "<unknown step>";

        string? label = step.LabelOptions?.Label;
        return label is null ? step.Name : $"{step.Name}  [{label}]";
    }

    private int GetActiveIteration(string stage, int stepId)
        => stepIterations.TryGetValue(GetStepKey(stage, stepId), out int iteration) ? iteration : 1;

    private static string GetStepMarker(int stepId, int iteration)
    {
        string builder = $"[#{stepId}";
        if (iteration > 1)
            builder += $":r{iteration}";

        return builder + "]";
    }

    private static string MapStateLabel(DebugLifecycleState state)
    {
        return state switch
        {
            DebugLifecycleState.Complete => "PASS",
            DebugLifecycleState.Error => "FAIL",
            DebugLifecycleState.Timeout => "TIMEOUT",
            DebugLifecycleState.Skipped => "SKIPPED",
            DebugLifecycleState.WaitingForRetry => "RETRY",
            DebugLifecycleState.Running => "RUNNING",
            _ => "STATE"
        };
    }

    private static string MapStateBadge(DebugLifecycleState state)
    {
        return state switch
        {
            DebugLifecycleState.Complete => "PASS",
            DebugLifecycleState.Error => "FAIL",
            DebugLifecycleState.Timeout => "TIME",
            DebugLifecycleState.Skipped => "SKIP",
            DebugLifecycleState.WaitingForRetry => "WAIT",
            DebugLifecycleState.Running => "RUN ",
            _ => "INFO"
        };
    }

    private static DebugValueKind MapValueKind(StepIOKind kind)
        => kind == StepIOKind.Variable ? DebugValueKind.Variable : DebugValueKind.Artifact;

    private static string GetValueKindLabel(DebugValueKind kind)
        => kind == DebugValueKind.Variable ? "Variable" : "Artifact";

    private static IReadOnlyList<ExecutionLayerPlan> BuildExecutionLayers(DebugStepState[] steps)
    {
        Dictionary<int, List<int>> dependents = Enumerable.Range(0, steps.Length).ToDictionary(index => index, _ => new List<int>());
        Dictionary<int, int> indegree = Enumerable.Range(0, steps.Length).ToDictionary(index => index, _ => 0);

        for (int leftIndex = 0; leftIndex < steps.Length; leftIndex++)
        {
            for (int rightIndex = leftIndex + 1; rightIndex < steps.Length; rightIndex++)
            {
                if (!RequiresSequentialOrdering(steps[leftIndex], steps[rightIndex]))
                    continue;

                dependents[leftIndex].Add(rightIndex);
                indegree[rightIndex]++;
            }
        }

        List<ExecutionLayerPlan> layers = [];
        HashSet<int> scheduled = [];

        while (scheduled.Count < steps.Length)
        {
            int[] readyStepIds = indegree
                .Where(entry => !scheduled.Contains(entry.Key) && entry.Value == 0)
                .Select(entry => entry.Key)
                .OrderBy(index => index)
                .ToArray();

            if (readyStepIds.Length == 0)
                throw new TestFramework.Core.Exceptions.DependencyGraphException("Unable to derive debugger-visible execution layers for the stage.");

            int layerIndex = layers.Count;
            layers.Add(new ExecutionLayerPlan(layerIndex, readyStepIds, steps[readyStepIds[0]].Phase));

            foreach (int readyStepId in readyStepIds)
            {
                scheduled.Add(readyStepId);
                foreach (int dependent in dependents[readyStepId])
                    indegree[dependent]--;
            }
        }

        return layers;
    }

    private static bool RequiresSequentialOrdering(DebugStepState left, DebugStepState right)
    {
        if (RequiresPhaseOrdering(left, right))
            return true;

        if (left.ExecutionOptions.ParallelizationMode == StepParallelizationMode.DoNotParallelize || right.ExecutionOptions.ParallelizationMode == StepParallelizationMode.DoNotParallelize)
            return true;

        return HasAccessConflict(left.IOContract, right.IOContract);
    }

    private static bool RequiresPhaseOrdering(DebugStepState left, DebugStepState right)
    {
        if (left.Phase != right.Phase)
            return true;

        return !IsMergeablePhase(left.Phase);
    }

    private static bool IsMergeablePhase(StepExecutionPhase phase)
    {
        return phase is StepExecutionPhase.Prepare or StepExecutionPhase.Materialize;
    }

    private static bool HasAccessConflict(StepIOContract left, StepIOContract right)
    {
        foreach (StepIOEntry leftOutput in left.Outputs)
        {
            if (ContainsEntry(right.Inputs, leftOutput) || ContainsEntry(right.Outputs, leftOutput))
                return true;
        }

        foreach (StepIOEntry leftInput in left.Inputs)
        {
            if (ContainsEntry(right.Outputs, leftInput))
                return true;
        }

        return false;
    }

    private static bool ContainsEntry(IEnumerable<StepIOEntry> entries, StepIOEntry candidate)
    {
        return entries.Any(entry => entry.Kind == candidate.Kind && StringComparer.Ordinal.Equals(entry.Key, candidate.Key));
    }

    private sealed class StageRenderState
    {
        private readonly Dictionary<string, StepRenderState> stepsByKey = new(System.StringComparer.Ordinal);
        private readonly Dictionary<int, int> layerIndexByStepId = new();
        private readonly Dictionary<int, PhaseRunInfo> phaseRunByLayerIndex = new();

        private readonly IReadOnlyDictionary<string, VariableState> variablesByKey;
        private readonly IReadOnlyDictionary<string, ArtifactState> artifactsByKey;

        public StageRenderState(string name, DebugStageState? stageDefinition, IReadOnlyDictionary<string, VariableState> variablesByKey, IReadOnlyDictionary<string, ArtifactState> artifactsByKey)
        {
            Name = name;
            StageDefinition = stageDefinition;
            this.variablesByKey = variablesByKey;
            this.artifactsByKey = artifactsByKey;
            LayerPlans = stageDefinition is null ? [] : BuildExecutionLayers(stageDefinition.Steps);

            for (int index = 0; index < LayerPlans.Count; index++)
            {
                foreach (int stepId in LayerPlans[index].StepIds)
                    layerIndexByStepId[stepId] = index;
            }

            SeedPhaseRuns();
        }

        public string Name { get; }
        public DebugStageState? StageDefinition { get; }
        public IReadOnlyList<ExecutionLayerPlan> LayerPlans { get; }
        public string Description => StageDefinition?.Description ?? string.Empty;
        public int StepCount => StageDefinition?.Steps.Length ?? 0;
        public List<FlowEventRenderState> FlowEvents { get; } = [];
        public List<string> LogLines { get; } = [];
        public List<StepRenderState> OrderedSteps { get; } = [];

        public StepRenderState StartStep(int stepId, int iteration, DebugStepState? definition, string displayName)
        {
            StepRenderState step = new(stepId, iteration, definition, displayName, TryGetLayerIndex(stepId, out int layerIndex) ? $"L{layerIndex}" : null);
            step.CaptureInputs(variablesByKey, artifactsByKey);
            stepsByKey[step.Key] = step;
            OrderedSteps.Add(step);
            return step;
        }

        public StepRenderState GetOrCreateStep(int stepId, int iteration, DebugStepState? definition, string displayName)
        {
            if (stepsByKey.TryGetValue(StepRenderState.GetKey(stepId, iteration), out StepRenderState? existing))
                return existing;

            return StartStep(stepId, iteration, definition, displayName);
        }

        public StepRenderState? TryGetStep(int stepId, int iteration)
            => stepsByKey.GetValueOrDefault(StepRenderState.GetKey(stepId, iteration));

        public bool TryGetLayerIndex(int stepId, out int layerIndex)
            => layerIndexByStepId.TryGetValue(stepId, out layerIndex);

        public bool TryCreateLayerBanner(int layerIndex, out string? banner)
        {
            banner = null;
            if (layerIndex < 0 || layerIndex >= LayerPlans.Count)
                return false;

            ExecutionLayerPlan layer = LayerPlans[layerIndex];
            PhaseRunInfo phaseRun = phaseRunByLayerIndex[layerIndex];

            if (layer.StepIds.Length > 1)
            {
                banner = $"> L{layer.LayerIndex}  {layer.Phase}  x{layer.StepIds.Length}";
                return true;
            }

            if (!phaseRun.IsFirstLayer || phaseRun.TotalSteps <= 1)
                return false;

            banner = $"> {layer.Phase} phase  x{phaseRun.TotalSteps}";
            return true;
        }

        public IReadOnlyList<StepGroupRenderState> GetOrderedStepGroups()
        {
            List<StepGroupRenderState> groups = [];
            HashSet<int> emittedStepIds = [];

            foreach (StepRenderState attempt in OrderedSteps)
            {
                if (!emittedStepIds.Add(attempt.StepId))
                    continue;

                List<StepRenderState> attempts = [.. OrderedSteps.Where(candidate => candidate.StepId == attempt.StepId)];
                groups.Add(new StepGroupRenderState(attempts));
            }

            return groups;
        }

        private void SeedPhaseRuns()
        {
            int layerIndex = 0;
            while (layerIndex < LayerPlans.Count)
            {
                StepExecutionPhase phase = LayerPlans[layerIndex].Phase;
                int runStart = layerIndex;
                int totalSteps = 0;

                while (layerIndex < LayerPlans.Count && LayerPlans[layerIndex].Phase == phase)
                {
                    totalSteps += LayerPlans[layerIndex].StepIds.Length;
                    layerIndex++;
                }

                for (int index = runStart; index < layerIndex; index++)
                    phaseRunByLayerIndex[index] = new PhaseRunInfo(index == runStart, totalSteps);
            }
        }
    }

    private void WriteSectionHeader(string title, BoxPrefix prefix)
    {
        WriteTitledBorder(title, '-', prefix);
    }

    private void WriteTitledBorder(string title, char fill, BoxPrefix prefix)
    {
        string trimmedTitle = title.Length > PanelInnerWidth - 2 ? title[..(PanelInnerWidth - 2)] : title;
        int remaining = PanelInnerWidth - trimmedTitle.Length - 1;
        char line = MapHorizontal(fill);
        writer.WriteLine($"{prefix.Top}{TitleLeftCorner}{line} {trimmedTitle} {new string(line, Math.Max(0, remaining))}{TitleRightCorner}");
    }

    private void WriteTopBorder(char fill, BoxPrefix prefix)
    {
        char line = MapHorizontal(fill);
        writer.WriteLine($"{prefix.Top}{TopLeftCorner(fill)}{new string(line, PanelWidth - 2)}{TopRightCorner(fill)}");
    }

    private void WriteBottomBorder(char fill, BoxPrefix prefix)
    {
        char line = MapHorizontal(fill);
        writer.WriteLine($"{prefix.Bottom}{BottomLeftCorner(fill)}{new string(line, PanelWidth - 2)}{BottomRightCorner(fill)}");
    }

    private void WriteSeparator(BoxPrefix prefix)
    {
        writer.WriteLine($"{prefix.Rest}{SeparatorLeft}{new string(SeparatorHorizontal, PanelWidth - 2)}{SeparatorRight}");
    }

    private void WriteKeyValueContent(string left, string right, BoxPrefix prefix, bool connectToBox)
    {
        string safeLeft = left.Length > PanelInnerWidth ? left[..PanelInnerWidth] : left;
        int spacing = PanelInnerWidth - safeLeft.Length - right.Length;
        if (spacing >= 1)
        {
            WriteContentLine($"{safeLeft}{new string(' ', spacing)}{right}", prefix, connectToBox);
            return;
        }

        WriteWrappedContent(left, prefix, connectToBox);
        WriteWrappedContent(right, prefix, false);
    }

    private void WriteWrappedContent(string content, BoxPrefix prefix, bool connectToBox)
    {
        bool isFirstLine = true;
        foreach (string line in WrapText(content, PanelInnerWidth))
        {
            WriteContentLine(line, prefix, connectToBox && isFirstLine);
            isFirstLine = false;
        }
    }

    private void WriteContentLine(string content, BoxPrefix prefix, bool connectToBox)
    {
        char leftBoundary = connectToBox ? ContentJoinLeft : OuterVertical;
        string leftPrefix = connectToBox ? prefix.FirstContent : prefix.Rest;
        writer.WriteLine($"{leftPrefix}{leftBoundary} {content.PadRight(PanelInnerWidth)} {OuterVertical}");
    }

    private void WriteGapLine(string prefix)
    {
        writer.WriteLine(prefix.TrimEnd());
    }

    private static BoxPrefix CreateRootBoxPrefix()
        => new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

    private BoxPrefix CreateBoxPrefix(string ancestorPrefix, bool hasFollowingSibling)
    {
        string gap = ancestorPrefix + TreeTrunkPrefix;
        string branch = hasFollowingSibling ? TreeBranchPrefix : TreeLastBranchPrefix;
        string rest = ancestorPrefix + (hasFollowingSibling ? TreeTrunkPrefix : TreeGapPrefix);
        return new(gap, ancestorPrefix + branch, rest, rest, gap);
    }

    private string TreeTrunkPrefix => useAsciiOutput ? "| " : "│ ";

    private string TreeGapPrefix => "  ";

    private string TreeBranchPrefix => useAsciiOutput ? "|-" : "├─";

    private string TreeLastBranchPrefix => useAsciiOutput ? "\\-" : "└─";

    private char OuterVertical => useAsciiOutput ? '|' : '│';

    private char ContentJoinLeft => useAsciiOutput ? '|' : '┤';

    private char InnerVertical => useAsciiOutput ? '|' : '│';

    private char SeparatorHorizontal => useAsciiOutput ? '-' : '─';

    private char TitleLeftCorner => useAsciiOutput ? '+' : '╭';

    private char TitleRightCorner => useAsciiOutput ? '+' : '╮';

    private char SeparatorLeft => useAsciiOutput ? '|' : '├';

    private char SeparatorRight => useAsciiOutput ? '|' : '┤';

    private char TopLeftCorner(char fill)
        => useAsciiOutput ? '+' : '╭';

    private char TopRightCorner(char fill)
        => useAsciiOutput ? '+' : '╮';

    private char BottomLeftCorner(char fill)
        => useAsciiOutput ? '+' : '╰';

    private char BottomRightCorner(char fill)
        => useAsciiOutput ? '+' : '╯';

    private char MapHorizontal(char fill)
    {
        if (useAsciiOutput)
            return fill;

        return '─';
    }

    private readonly record struct BoxPrefix(string Top, string FirstContent, string Rest, string Bottom, string Gap);

    private static bool IsUnicodeOutputDisabled()
    {
        string? value = global::System.Environment.GetEnvironmentVariable(DisableUnicodeOutEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (bool.TryParse(value, out bool parsed))
            return parsed;

        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldRenderSideBySide(IReadOnlyCollection<string> leftLines, IReadOnlyCollection<string> rightLines)
    {
        return leftLines.Count <= 3
            && rightLines.Count <= 3
            && EstimateWrappedLineCount(leftLines, 44) <= 5
            && EstimateWrappedLineCount(rightLines, 44) <= 5;
    }

    private static int EstimateWrappedLineCount(IEnumerable<string> lines, int width)
    {
        int count = 1;
        foreach (string line in lines)
            count += WrapText($"- {line}", width).Count();

        return count;
    }

    private static string[] FlattenForColumn(string title, IEnumerable<string> lines, int width)
    {
        List<string> result = [title.ToUpperInvariant()];
        foreach (string line in lines)
            result.AddRange(WrapText($"- {line}", width));

        return [.. result];
    }

    private static IEnumerable<string> WrapText(string content, int width)
    {
        if (string.IsNullOrEmpty(content))
        {
            yield return string.Empty;
            yield break;
        }

        foreach (string rawLine in content.Split(["\r\n", "\n", "\r"], StringSplitOptions.None))
        {
            string remaining = rawLine;
            while (remaining.Length > width)
            {
                int splitAt = remaining.LastIndexOf(' ', width);
                if (splitAt <= 0)
                    splitAt = width;

                yield return remaining[..splitAt].TrimEnd();
                remaining = remaining[splitAt..].TrimStart();
            }

            yield return remaining;
        }
    }

    private sealed class StepRenderState
    {
        public StepRenderState(int stepId, int iteration, DebugStepState? definition, string displayName, string? layerDisplay)
        {
            StepId = stepId;
            Iteration = iteration;
            Definition = definition;
            DisplayName = displayName;
            LayerDisplay = layerDisplay;
        }

        public static string GetKey(int stepId, int iteration) => $"{stepId}:{iteration}";

        public string Key => GetKey(StepId, Iteration);
        public int StepId { get; }
        public int Iteration { get; }
        public string Marker => GetStepMarker(StepId, Iteration);
        public DebugStepState? Definition { get; }
        public string DisplayName { get; }
        public string? LayerDisplay { get; }
        public DebugLifecycleState State { get; set; } = DebugLifecycleState.Running;
        public bool Completed { get; set; }
        public List<InputSnapshotRenderState> InputSnapshots { get; } = [];
        public List<string> LogLines { get; } = [];
        public List<ValueUpdateRenderState> ValueUpdates { get; } = [];

        public void CaptureInputs(IReadOnlyDictionary<string, VariableState> variablesByKey, IReadOnlyDictionary<string, ArtifactState> artifactsByKey)
        {
            InputSnapshots.Clear();

            if (Definition is null)
                return;

            foreach (StepIOEntry input in Definition.IOContract.Inputs)
            {
                string? displayText = input.Kind switch
                {
                    StepIOKind.Variable => variablesByKey.GetValueOrDefault(input.Key)?.Envelope.DisplayText,
                    StepIOKind.Artifact => artifactsByKey.GetValueOrDefault(input.Key)?.Envelope.DisplayText,
                    _ => null
                };

                InputSnapshots.Add(new InputSnapshotRenderState(input.Kind, input.Key, input.Required, displayText));
            }
        }
    }

    private sealed class StepGroupRenderState
    {
        public StepGroupRenderState(IReadOnlyList<StepRenderState> attempts)
        {
            Attempts = attempts;
        }

        public IReadOnlyList<StepRenderState> Attempts { get; }
        public StepRenderState FirstAttempt => Attempts[0];
        public StepRenderState LastAttempt => Attempts[^1];
        public string Marker => $"[#{FirstAttempt.StepId}]";
        public string DisplayName => FirstAttempt.DisplayName;
        public string PhaseDisplay => FirstAttempt.Definition?.Phase.ToString() ?? "Unknown";
        public string? LayerDisplay => FirstAttempt.LayerDisplay;
    }

    private sealed record FlowEventRenderState(string Badge, string Marker, string Message, int? StepId, bool StartsExecution);
    private sealed record InputSnapshotRenderState(StepIOKind Kind, string Key, bool Required, string? DisplayText);
    private sealed record ValueUpdateRenderState(string Name, DebugValueKind ValueKind, string DisplayText);
    private sealed record ExecutionLayerPlan(int LayerIndex, int[] StepIds, StepExecutionPhase Phase);
    private sealed record PhaseRunInfo(bool IsFirstLayer, int TotalSteps);
}