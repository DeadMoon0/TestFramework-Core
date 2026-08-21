using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using Xunit.Abstractions;

namespace TestFramework.Core.Debugger;

internal sealed class OutputRunDebugger : IRunDebugger, ISupportsRenderedLog
{
    private const string DisableUnicodeOutEnvironmentVariable = "TestFramework_Disable_Unicode_Out";
    private const int PanelWidth = 95;
    private const int MinimumPanelWidth = 16;

    /// <summary>
    /// The indentation a tab in content stands for.
    /// </summary>
    /// <remarks>
    /// Two spaces, because that is what the log's own nesting indents by: an indented log line keeps the
    /// depth it was written with instead of gaining whatever tab stop the reader's terminal has.
    /// </remarks>
    private const string TabIndent = "  ";

    /// <summary>A separator with no column divider crossing it.</summary>
    private const int NoJunction = -1;

    /// <summary>
    /// What the remainder of a line too long for the panel is indented by.
    /// </summary>
    /// <remarks>
    /// Flush against the border, the tail of a wrapped list entry reads as an entry of its own, which is
    /// the one thing a reader of this view must not have to second-guess.
    /// </remarks>
    private const string ContinuationIndent = "  ";

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
    private readonly Dictionary<string, DebugValue> variablesByKey = new(System.StringComparer.Ordinal);
    private readonly Dictionary<string, DebugValue> artifactsByKey = new(System.StringComparer.Ordinal);
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
            HandleInitTimelineRun(name, projectPath, runStructure, identity);
        }

        return Task.CompletedTask;
    }

    private void HandleInitTimelineRun(string name, string projectPath, TimelineRunStructure runStructure, TestIdentity? identity)
    {
        runName = name;

        // The identity's answer first. The announced path is the host process under a test runner,
        // so taking it at face value put "testhost.exe" on the first line every reader sees.
        //
        // The name rather than the path: a full path wraps over three lines of the panel and buries
        // the one word that identifies the run among directories every line of the log shares.
        this.projectPath = TestIdentity.ShortNameOf(identity?.ProjectDisplayName ?? projectPath);
        this.runStructure = runStructure;
        stepIterations.Clear();
        orderedStages.Clear();
        stagesByName.Clear();
        variablesByKey.Clear();
        artifactsByKey.Clear();
        runLogLines.Clear();
        assertionLines.Clear();

        foreach (DebugValue variable in runStructure.Variables.Values)
            variablesByKey[variable.Key] = variable;

        foreach (DebugValue artifact in runStructure.Artifacts.Values)
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
                iteration == 1 ? "RUN" : "RETRY",
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
                variablesByKey[name] = new DebugValue { Key = name, Envelope = value };
                break;
            case DebugValueKind.Artifact:
                artifactsByKey[name] = new DebugValue { Key = name, Envelope = value };
                break;
        }

        if (stage is null || stepId is null)
            return;

        StepRenderState? stepRun = FindActiveStep(stage, stepId.Value);
        if (stepRun is null)
            return;

        stepRun.ValueUpdates.Add(new ValueUpdateRenderState(name, valueKind, Render(value)));
    }

    /// <summary>
    /// One line for a value: what it is, and where it went when it did not fit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When the value is in a file the line says what it <em>is</em> — its type and its size — and
    /// where to find it. It deliberately does not recite the first hundred characters of it: an
    /// excerpt of a large response body is the part least likely to contain what a reader is looking
    /// for, and it buries the step flow the output exists to show. The file is readable by anything,
    /// including the build that publishes it.
    /// </para>
    /// <para>
    /// When the value fits, the summary is the value, and there is nothing to point at.
    /// </para>
    /// </remarks>
    private static string Render(DebugValueEnvelope envelope)
    {
        if (envelope.Description.Body is not { } body)
            return envelope.Description.Summary;

        return $"{Facts(envelope)}  -> {body.RelativePath} ({Size(body.SizeInBytes)})";
    }

    /// <summary>What the value is, from the facts it was described with.</summary>
    /// <remarks>
    /// The summary when there are no facts: a value can be described in one line and nothing else — a null, a
    /// number — and printing an empty list of facts for it would say less than the line does.
    /// </remarks>
    private static string Facts(DebugValueEnvelope envelope)
    {
        DebugValueField[] fields = envelope.Description.Fields;

        return fields.Length == 0
            ? envelope.Description.Summary
            : string.Join(", ", fields.Select(field => $"{field.Name} {field.Text}"));
    }

    private static string Size(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024d:0.#} KB",
        _ => $"{bytes / (1024d * 1024d):0.#} MB"
    };

    private static string? RenderOrNull(DebugValueEnvelope? envelope) => envelope is null ? null : Render(envelope);

    /// <summary>
    /// Ignored.
    /// </summary>
    /// <remarks>
    /// This class is the console, and the console renders from the events themselves — see
    /// <see cref="WriteRenderedLog"/>. A transported entry carries the facts for a consumer that has to build
    /// its own display; printing it here as well would print everything twice, in a worse form.
    /// </remarks>
    public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry) => Task.CompletedTask;

    /// <summary>
    /// Files a rendered event under the run, a stage, or one attempt at a step.
    /// </summary>
    /// <remarks>
    /// Lines against a step whose attempt is no longer the active one are dropped: they belong to an attempt
    /// this renderer has already closed, and appending them to the current one would report them against the
    /// wrong try.
    /// </remarks>
    public void WriteRenderedLog(string[] lines, LogPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(lines);

        lock (renderGate)
        {
            if (placement.Stage is null)
            {
                Collect(runLogLines, lines);
                return;
            }

            if (placement.StepId is null || placement.Iteration is null)
            {
                Collect(EnsureStage(placement.Stage).LogLines, lines);
                return;
            }

            string stepKey = GetStepKey(placement.Stage, placement.StepId.Value);

            if (!stepIterations.TryGetValue(stepKey, out int activeIteration) || activeIteration != placement.Iteration.Value)
                return;

            StepRenderState? stepRun = FindActiveStep(placement.Stage, placement.StepId.Value, placement.Iteration.Value);

            if (stepRun is not null)
                Collect(stepRun.LogLines, lines);
        }
    }

    private static void Collect(List<string> destination, string[] lines)
    {
        foreach (string line in lines)
            destination.Add(string.IsNullOrWhiteSpace(line) ? string.Empty : line);
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
        // Rendered here, from the check's name and arguments. The transport carries those; the sentence a
        // console prints about them is the console's own business.
        assertionLines.Add($"{(entry.Succeeded ? "[PASS]" : "[FAIL]")} {entry.Target}  {Call(entry)}");

        if (!entry.Succeeded)
            assertionLines.Add($"       {Comparison(entry)}");
    }

    /// <summary>
    /// The check as it would be written in a test.
    /// </summary>
    /// <remarks>
    /// Strings are quoted, because the line is meant to read like the call the test made. That is a decision
    /// about how to print an argument, which is why it lives here and not in what the transport carries.
    /// </remarks>
    private static string Call(DebugAssertionEntry entry)
        => entry.Arguments.Length == 0
            ? entry.AssertionName
            : $"{entry.AssertionName}({string.Join(", ", entry.Arguments.Select(Argument))})";

    /// <summary>Why a check did not hold, put together from what it expected and what it found.</summary>
    private static string Comparison(DebugAssertionEntry entry)
    {
        string was = $"was {entry.Actual.Summary}";

        return entry.Arguments.Length == 0
            ? was
            : $"expected {string.Join(", ", entry.Arguments.Select(Argument))}, {was}";
    }

    private static string Argument(DebugLogField field)
        => field.Value.Type == JTokenType.String
            ? $"\"{DebugLogTemplate.Text(field)}\""
            : DebugLogTemplate.Text(field);

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

    /// <summary>
    /// Lists every value the run wrote out, once, at the end.
    /// </summary>
    /// <remarks>
    /// Scattered through the step panels, a run's files are found only by whoever reads all of them.
    /// Collected here they are a manifest: what this run produced, how big, and where — which is what
    /// someone reads a failed build's log for, and what tells them which artifact to open.
    /// </remarks>
    private List<string> ValueFileLines() =>
    [
        .. variablesByKey.Values
            .Concat(artifactsByKey.Values)
            .Where(value => value.Envelope.Description.Body is not null)
            .OrderBy(value => value.Key, System.StringComparer.Ordinal)
            .Select(value => $"{value.Key}  {Size(value.Envelope.Description.Body!.SizeInBytes)}  {value.Envelope.Description.Body!.RelativePath}")
    ];

    private void RenderValueFiles(List<string> lines)
    {
        if (lines.Count == 0)
            return;

        BoxPrefix prefix = CreateBoxPrefix(string.Empty, assertionLines.Count > 0);
        WriteGapLine(prefix.Gap);
        RenderSection("Value Files", lines, prefix);
    }

    private void RenderRunSummary()
    {
        BoxPrefix runPrefix = CreateRootBoxPrefix();
        WriteTopBorder('=', runPrefix);
        WriteWrappedContent($"TIMELINE DEBUG VIEW  {runName ?? "<unnamed>"}", runPrefix, false);
        if (!string.IsNullOrWhiteSpace(projectPath))
            WriteWrappedContent($"Project: {projectPath}", runPrefix, false);
        WriteBottomBorder('=', runPrefix);

        // Worked out up front because the box drawing needs to know whether anything follows: a
        // section that decides for itself whether to appear leaves the one before it drawn as last.
        List<string> valueFileLines = ValueFileLines();
        bool tail = valueFileLines.Count > 0 || assertionLines.Count > 0;

        if (runLogLines.Count > 0)
        {
            BoxPrefix runLogPrefix = CreateBoxPrefix(string.Empty, orderedStages.Count > 0 || tail);
            WriteGapLine(runLogPrefix.Gap);
            RenderSection("Run Log", runLogLines, runLogPrefix);
        }

        for (int stageIndex = 0; stageIndex < orderedStages.Count; stageIndex++)
        {
            bool hasFollowingSibling = stageIndex < orderedStages.Count - 1 || tail;
            BoxPrefix stagePrefix = CreateBoxPrefix(string.Empty, hasFollowingSibling);
            WriteGapLine(stagePrefix.Gap);
            RenderStage(orderedStages[stageIndex], stagePrefix);
        }

        RenderValueFiles(valueFileLines);

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

        string childAncestorPrefix = stagePrefix.Rest;

        // Its own child prefix, not the stage's: drawn at the stage's own level it repeated the branch
        // connector of a node that already had one, and claimed the stage's width for a box one level
        // deeper. The flow trace always follows it, so it always has a sibling below.
        if (stage.LogLines.Count > 0)
        {
            BoxPrefix activityPrefix = CreateBoxPrefix(childAncestorPrefix, true);
            WriteGapLine(activityPrefix.Gap);
            RenderSection("Stage Activity", stage.LogLines, activityPrefix);
        }

        BoxPrefix flowTracePrefix = CreateBoxPrefix(childAncestorPrefix, stepGroups.Count > 0);
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

        int closingJunction = NoJunction;

        if (ShouldRenderSideBySide(inputLines, outputLines, LeftColumnWidth(stepPrefix)))
            closingJunction = RenderDualBoxSection("Inputs", inputLines, "Outputs", outputLines, stepPrefix);
        else
        {
            RenderBoxSection("Inputs", inputLines, stepPrefix);
            RenderBoxSection("Outputs", outputLines, stepPrefix);
        }

        foreach (StepRenderState attempt in stepGroup.Attempts)
        {
            RenderBoxSection($"Logs Attempt {attempt.Iteration}", attempt.LogLines.Count == 0 ? ["(no log lines)"] : attempt.LogLines, stepPrefix, closingJunction);
            closingJunction = NoJunction;
        }

        RenderBoxSection("Final Result", BuildFinalResultLines(stepGroup), stepPrefix, closingJunction);
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

            // Padded outside the brackets: a five-letter badge is one column wider than a four-letter
            // one, so brackets that only hug the badge kept every retry row out of step with the rest.
            string badge = $"[{entry.Badge}]";
            WriteWrappedContent($"{index + 1,2}. {badge,-7} {entry.Marker,-8} {entry.Message}", prefix, connectToBox);
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

    /// <summary>
    /// One titled block inside a panel, opened by the separator that closes the block above it.
    /// </summary>
    /// <remarks>
    /// <c>closingJunction</c> is the column at which a divider from the block above meets that
    /// separator, or <see cref="NoJunction"/> when nothing has to be closed off.
    /// </remarks>
    private void RenderBoxSection(string title, IReadOnlyCollection<string> lines, BoxPrefix prefix, int closingJunction = NoJunction)
    {
        WriteSeparator(prefix, closingJunction, ColumnJoinBottom);
        WriteWrappedContent(title.ToUpperInvariant(), prefix, false);
        foreach (string line in lines)
            WriteWrappedContent($"- {line}", prefix, false);
    }

    /// <summary>Two blocks side by side, returning the column its divider has to be closed at.</summary>
    private int RenderDualBoxSection(string leftTitle, IReadOnlyCollection<string> leftLines, string rightTitle, IReadOnlyCollection<string> rightLines, BoxPrefix prefix)
    {
        int columnWidth = LeftColumnWidth(prefix);
        // The odd column goes to the right one, so the two columns and the divider between them always
        // add up to the panel's inner width instead of falling a column short at odd widths.
        int rightColumnWidth = InnerWidth(prefix) - 3 - columnWidth;
        string[] wrappedLeft = FlattenForColumn(leftTitle, leftLines, columnWidth);
        string[] wrappedRight = FlattenForColumn(rightTitle, rightLines, rightColumnWidth);
        int rowCount = Math.Max(wrappedLeft.Length, wrappedRight.Length);
        int junctionIndex = columnWidth + 2;

        WriteSeparator(prefix, junctionIndex, ColumnJoinTop);
        for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            string left = rowIndex < wrappedLeft.Length ? wrappedLeft[rowIndex] : string.Empty;
            string right = rowIndex < wrappedRight.Length ? wrappedRight[rowIndex] : string.Empty;
            writer.WriteLine($"{prefix.Rest}{OuterVertical} {left.PadRight(columnWidth)} {InnerVertical} {right.PadRight(rightColumnWidth)} {OuterVertical}");
        }

        return junctionIndex;
    }

    private string[] RenderInputLines(StepRenderState stepRun)
    {
        if (stepRun.InputSnapshots.Count > 0)
            return [.. stepRun.InputSnapshots.Select(RenderInputSnapshotLine)];

        DebugStepIo[] inputs = stepRun.Definition?.Inputs ?? [];
        if (inputs.Length == 0)
            return ["(none declared)"];

        return [.. inputs.Select(RenderInputLine)];
    }

    private string RenderInputLine(DebugStepIo entry)
    {
        string requirement = entry.Required ? "required" : "optional";
        string kindLabel = entry.Kind == StepIOKind.Variable ? "Variable" : "Artifact";
        string? value = entry.Kind == StepIOKind.Variable
            ? RenderOrNull(variablesByKey.GetValueOrDefault(entry.Key)?.Envelope)
            : RenderOrNull(artifactsByKey.GetValueOrDefault(entry.Key)?.Envelope);

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
        DebugStepIo[] declaredOutputs = stepRun.Definition?.Outputs ?? [];

        foreach (DebugStepIo output in declaredOutputs)
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

        string? label = step.Label;
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
            DebugLifecycleState.Running => "RUN",
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

        if (left.Parallelization == StepParallelizationMode.DoNotParallelize || right.Parallelization == StepParallelizationMode.DoNotParallelize)
            return true;

        return HasAccessConflict(left, right);
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

    private static bool HasAccessConflict(DebugStepState left, DebugStepState right)
    {
        foreach (DebugStepIo leftOutput in left.Outputs)
        {
            if (ContainsEntry(right.Inputs, leftOutput) || ContainsEntry(right.Outputs, leftOutput))
                return true;
        }

        foreach (DebugStepIo leftInput in left.Inputs)
        {
            if (ContainsEntry(right.Outputs, leftInput))
                return true;
        }

        return false;
    }

    private static bool ContainsEntry(IEnumerable<DebugStepIo> entries, DebugStepIo candidate)
    {
        return entries.Any(entry => entry.Kind == candidate.Kind && StringComparer.Ordinal.Equals(entry.Key, candidate.Key));
    }

    private sealed class StageRenderState
    {
        private readonly Dictionary<string, StepRenderState> stepsByKey = new(System.StringComparer.Ordinal);
        private readonly Dictionary<int, int> layerIndexByStepId = new();
        private readonly Dictionary<int, PhaseRunInfo> phaseRunByLayerIndex = new();

        private readonly IReadOnlyDictionary<string, DebugValue> variablesByKey;
        private readonly IReadOnlyDictionary<string, DebugValue> artifactsByKey;

        public StageRenderState(string name, DebugStageState? stageDefinition, IReadOnlyDictionary<string, DebugValue> variablesByKey, IReadOnlyDictionary<string, DebugValue> artifactsByKey)
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

    /// <summary>
    /// How wide a panel is at the depth its prefix puts it at.
    /// </summary>
    /// <remarks>
    /// A nested box is drawn to the right of its parent's tree prefix, so it has to give up the columns
    /// that prefix occupies. Drawing every box at the full width instead stepped each one two columns
    /// further right than the box containing it, and the view's right-hand border came out as a
    /// staircase rather than a line.
    /// </remarks>
    private static int BoxWidth(BoxPrefix prefix) => Math.Max(MinimumPanelWidth, PanelWidth - prefix.Rest.Length);

    /// <summary>The columns a panel has left for content once its borders and their padding are paid for.</summary>
    private static int InnerWidth(BoxPrefix prefix) => BoxWidth(prefix) - 4;

    /// <summary>The left column of a two-column section, whose divider costs three columns of its own.</summary>
    private static int LeftColumnWidth(BoxPrefix prefix) => (InnerWidth(prefix) - 3) / 2;

    /// <summary>
    /// Content measured the way it will be printed.
    /// </summary>
    /// <remarks>
    /// Log lines arrive with their indentation as tab characters, and a tab is one character to
    /// <see cref="string.PadRight(int)"/> and up to eight columns on screen. One indented log line was
    /// enough to push a panel's right border out of the column every other line held it in.
    /// </remarks>
    private static string ExpandTabs(string content)
        => content.Contains('\t') ? content.Replace("\t", TabIndent) : content;

    private void WriteTitledBorder(string title, char fill, BoxPrefix prefix)
    {
        int innerWidth = InnerWidth(prefix);
        string safeTitle = ExpandTabs(title);
        string trimmedTitle = safeTitle.Length > innerWidth - 2 ? safeTitle[..(innerWidth - 2)] : safeTitle;
        int remaining = innerWidth - trimmedTitle.Length - 1;
        char line = MapHorizontal(fill);
        writer.WriteLine($"{prefix.Top}{TitleLeftCorner}{line} {trimmedTitle} {new string(line, Math.Max(0, remaining))}{TitleRightCorner}");
    }

    private void WriteTopBorder(char fill, BoxPrefix prefix)
    {
        char line = MapHorizontal(fill);
        writer.WriteLine($"{prefix.Top}{TopLeftCorner(fill)}{new string(line, BoxWidth(prefix) - 2)}{TopRightCorner(fill)}");
    }

    private void WriteBottomBorder(char fill, BoxPrefix prefix)
    {
        char line = MapHorizontal(fill);
        writer.WriteLine($"{prefix.Bottom}{BottomLeftCorner(fill)}{new string(line, BoxWidth(prefix) - 2)}{BottomRightCorner(fill)}");
    }

    private void WriteSeparator(BoxPrefix prefix)
    {
        WriteSeparator(prefix, NoJunction, SeparatorHorizontal);
    }

    /// <summary>
    /// A separator across the panel, joined to a column divider where one meets it.
    /// </summary>
    /// <remarks>
    /// Without the junction the divider of a two-column section begins and ends against an unbroken
    /// rule, which reads as a line drawn over the box rather than as part of it.
    /// </remarks>
    private void WriteSeparator(BoxPrefix prefix, int junctionIndex, char junction)
    {
        char[] line = new string(SeparatorHorizontal, BoxWidth(prefix) - 2).ToCharArray();
        if (junctionIndex >= 0 && junctionIndex < line.Length)
            line[junctionIndex] = junction;

        writer.WriteLine($"{prefix.Rest}{SeparatorLeft}{new string(line)}{SeparatorRight}");
    }

    private void WriteKeyValueContent(string left, string right, BoxPrefix prefix, bool connectToBox)
    {
        int innerWidth = InnerWidth(prefix);
        string expandedLeft = ExpandTabs(left);
        string expandedRight = ExpandTabs(right);
        string safeLeft = expandedLeft.Length > innerWidth ? expandedLeft[..innerWidth] : expandedLeft;
        int spacing = innerWidth - safeLeft.Length - expandedRight.Length;
        if (spacing >= 1)
        {
            WriteContentLine($"{safeLeft}{new string(' ', spacing)}{expandedRight}", prefix, connectToBox);
            return;
        }

        WriteWrappedContent(left, prefix, connectToBox);
        WriteWrappedContent(right, prefix, false);
    }

    private void WriteWrappedContent(string content, BoxPrefix prefix, bool connectToBox)
    {
        bool isFirstLine = true;
        foreach (string line in WrapText(content, InnerWidth(prefix)))
        {
            WriteContentLine(line, prefix, connectToBox && isFirstLine);
            isFirstLine = false;
        }
    }

    private void WriteContentLine(string content, BoxPrefix prefix, bool connectToBox)
    {
        char leftBoundary = connectToBox ? ContentJoinLeft : OuterVertical;
        string leftPrefix = connectToBox ? prefix.FirstContent : prefix.Rest;
        writer.WriteLine($"{leftPrefix}{leftBoundary} {content.PadRight(InnerWidth(prefix))} {OuterVertical}");
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

    private char ColumnJoinTop => useAsciiOutput ? '+' : '┬';

    private char ColumnJoinBottom => useAsciiOutput ? '+' : '┴';

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

    private static bool ShouldRenderSideBySide(IReadOnlyCollection<string> leftLines, IReadOnlyCollection<string> rightLines, int columnWidth)
    {
        return leftLines.Count <= 3
            && rightLines.Count <= 3
            && EstimateWrappedLineCount(leftLines, columnWidth) <= 5
            && EstimateWrappedLineCount(rightLines, columnWidth) <= 5;
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
            string remaining = ExpandTabs(rawLine);
            string indent = string.Empty;

            while (indent.Length + remaining.Length > width)
            {
                int limit = Math.Max(1, width - indent.Length);
                int splitAt = remaining.LastIndexOf(' ', Math.Min(limit, remaining.Length - 1));
                if (splitAt <= 0)
                    splitAt = limit;

                yield return indent + remaining[..splitAt].TrimEnd();
                remaining = remaining[splitAt..].TrimStart();
                indent = ContinuationIndent;
            }

            yield return indent + remaining;
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

        public void CaptureInputs(IReadOnlyDictionary<string, DebugValue> variablesByKey, IReadOnlyDictionary<string, DebugValue> artifactsByKey)
        {
            InputSnapshots.Clear();

            if (Definition is null)
                return;

            foreach (DebugStepIo input in Definition.Inputs)
            {
                string? displayText = input.Kind switch
                {
                    StepIOKind.Variable => RenderOrNull(variablesByKey.GetValueOrDefault(input.Key)?.Envelope),
                    StepIOKind.Artifact => RenderOrNull(artifactsByKey.GetValueOrDefault(input.Key)?.Envelope),
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