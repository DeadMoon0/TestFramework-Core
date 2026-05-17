using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Debugger;

internal class DebuggingRunSession(IRunDebugger debugger)
{
    private readonly AsyncLocal<ExecutionContextInfo?> currentExecutionContext = new();
    private readonly AsyncLocal<IterationContextInfo?> currentIterationContext = new();

    internal string SessionId { get; } = Guid.NewGuid().ToString();
    internal IRunDebugger Debugger { get; set; } = debugger;

    internal async Task InitSessionAsync(TimelineRunStructure runStructure)
    {
        await Debugger.SignalInitTimelineRunAsync(SessionId, GetTestName(), GetProjectPath(), runStructure);
    }

    internal Task TransitionRunAsync(DebugLifecycleState state, DebugLifecycleState? previousState = null)
    {
        return Debugger.SignalEntityTransitionAsync(SessionId, DebugEntityKind.Run, null, null, state, previousState);
    }

    internal Task TransitionStageAsync(string stage, DebugLifecycleState state, DebugLifecycleState? previousState = null)
    {
        return Debugger.SignalEntityTransitionAsync(SessionId, DebugEntityKind.Stage, stage, null, state, previousState);
    }

    internal Task TransitionStepAsync(string stage, int stepId, DebugLifecycleState state, DebugLifecycleState? previousState = null, DebugLifecycleState? outcomeState = null)
    {
        return Debugger.SignalEntityTransitionAsync(SessionId, DebugEntityKind.Step, stage, stepId, state, previousState, outcomeState);
    }

    internal Task UpdateVariableAsync(VariableIdentifier identifier, VariableState state)
    {
        return Debugger.SignalValueUpdateAsync(SessionId, identifier, DebugValueKind.Variable, currentExecutionContext.Value?.Stage, currentExecutionContext.Value?.StepId, state.Envelope);
    }

    internal Task UpdateArtifactAsync(ArtifactIdentifier identifier, ArtifactState state)
    {
        return Debugger.SignalValueUpdateAsync(SessionId, identifier, DebugValueKind.Artifact, currentExecutionContext.Value?.Stage, currentExecutionContext.Value?.StepId, state.Envelope);
    }

    internal Task LogAsync(DebugLogEntry entry)
    {
        ExecutionContextInfo? context = currentExecutionContext.Value;
        IterationContextInfo? iteration = currentIterationContext.Value;
        if (context is null || iteration is null)
            throw new InvalidOperationException("Structured log entries may only be emitted while a step iteration context is active.");

        DebugLogEntry entryWithContext = new()
        {
            OccurredAtUtc = entry.OccurredAtUtc == default ? DateTimeOffset.UtcNow : entry.OccurredAtUtc,
            Level = entry.Level,
            EventName = entry.EventName,
            Message = entry.Message,
            Lines = entry.Lines,
            IndentLevel = entry.IndentLevel,
            Stage = entry.Stage ?? context?.Stage,
            StepId = entry.StepId ?? context?.StepId,
            Iteration = entry.Iteration ?? iteration.Iteration,
            AssertionScope = entry.AssertionScope
        };

        return Debugger.SignalLogEntryAsync(SessionId, entryWithContext);
    }

    internal Task SignalAssertionAsync(DebugAssertionEntry entry)
    {
        return Debugger.SignalAssertionAsync(SessionId, new DebugAssertionEntry
        {
            OccurredAtUtc = entry.OccurredAtUtc == default ? DateTimeOffset.UtcNow : entry.OccurredAtUtc,
            TargetKind = entry.TargetKind,
            Target = entry.Target,
            AssertionName = entry.AssertionName,
            AssertionDisplay = entry.AssertionDisplay,
            Succeeded = entry.Succeeded,
            Expected = entry.Expected,
            Actual = entry.Actual,
            FailureReason = entry.FailureReason,
            AssertionScope = entry.AssertionScope
        });
    }

    internal Task FinishSessionAsync()
    {
        return Debugger.SignalTimelineRunFinishedAsync(SessionId);
    }

    internal async Task WaitWhenBreakpointHit(string stage, int index)
    {
        await Debugger.SignalAndWaitBreakpointHitAsync(SessionId, stage, index);
    }

    internal IDisposable BeginStepExecutionContext(string stage, int stepId)
    {
        var previous = currentExecutionContext.Value;
        currentExecutionContext.Value = new ExecutionContextInfo(stage, stepId);
        return new ExecutionContextScope(currentExecutionContext, previous);
    }

    internal IDisposable BeginStepIterationContext(int iteration)
    {
        var previous = currentIterationContext.Value;
        currentIterationContext.Value = new IterationContextInfo(iteration);
        return new IterationContextScope(currentIterationContext, previous);
    }

    private static string GetTestName()
    {
        return AppDomain.CurrentDomain.FriendlyName;
    }

    private static string GetProjectPath()
    {
        return Assembly.GetEntryAssembly()?.Location
            ?? System.Environment.ProcessPath
            ?? Path.Combine(AppContext.BaseDirectory, AppDomain.CurrentDomain.FriendlyName);
    }

    private sealed record ExecutionContextInfo(string Stage, int StepId);

    private sealed record IterationContextInfo(int Iteration);

    private sealed class ExecutionContextScope(AsyncLocal<ExecutionContextInfo?> context, ExecutionContextInfo? previous) : IDisposable
    {
        public void Dispose()
        {
            context.Value = previous;
        }
    }

    private sealed class IterationContextScope(AsyncLocal<IterationContextInfo?> context, IterationContextInfo? previous) : IDisposable
    {
        public void Dispose()
        {
            context.Value = previous;
        }
    }
}