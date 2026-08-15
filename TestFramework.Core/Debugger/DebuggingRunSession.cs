using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Debugger;

internal class DebuggingRunSession(IRunDebugger debugger)
{
    private readonly AsyncLocal<ExecutionContextInfo?> currentExecutionContext = new();
    private readonly AsyncLocal<IterationContextInfo?> currentIterationContext = new();
    private bool sessionInitialized;

    internal string SessionId { get; } = Guid.NewGuid().ToString();
    internal IRunDebugger Debugger { get; } = debugger;

    /// <summary>
    /// Gets a value indicating whether anything downstream will use the signals this session emits.
    /// Callers gate expensive signal preparation on this.
    /// </summary>
    internal bool IsCapturing => Debugger.IsCapturing;

    internal async Task InitSessionAsync(TimelineRunStructure runStructure)
    {
        // Naming the run walks the whole stack and resolving the project path scans loaded assemblies.
        // Neither is cheap, and neither has a reader when nothing is capturing.
        if (!IsCapturing)
        {
            sessionInitialized = true;
            return;
        }

        await Debugger.SignalInitTimelineRunAsync(SessionId, GetTestName(), GetProjectPath(), runStructure);
        sessionInitialized = true;
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

    internal void PublishVariableUpdate(VariableIdentifier identifier, VariableState state)
    {
        if (!sessionInitialized)
            return;

        PublishNonBlocking(Debugger.SignalValueUpdateAsync(SessionId, identifier, DebugValueKind.Variable, currentExecutionContext.Value?.Stage, currentExecutionContext.Value?.StepId, state.Envelope));
    }

    internal void PublishArtifactUpdate(ArtifactIdentifier identifier, ArtifactState state)
    {
        if (!sessionInitialized)
            return;

        PublishNonBlocking(Debugger.SignalValueUpdateAsync(SessionId, identifier, DebugValueKind.Artifact, currentExecutionContext.Value?.Stage, currentExecutionContext.Value?.StepId, state.Envelope));
    }

    internal Task LogAsync(DebugLogEntry entry)
    {
        ExecutionContextInfo? context = currentExecutionContext.Value;
        IterationContextInfo? iteration = currentIterationContext.Value;

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
            Iteration = entry.Iteration ?? iteration?.Iteration,
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

    /// <summary>
    /// The project path is a property of the process, so it cannot change between runs. Resolving it
    /// scans every loaded assembly; doing that once per process instead of once per run is free.
    /// </summary>
    private static readonly Lazy<string> CachedProjectPath = new(ResolveProjectPath, LazyThreadSafetyMode.ExecutionAndPublication);

    private static string GetProjectPath() => CachedProjectPath.Value;

    private static string GetTestName()
    {
        // Stays per-run: it resolves the test method currently on the stack, which differs per run.
        // File info is never read, so skip collecting it.
        MethodInfo? testMethod = new StackTrace(skipFrames: 1, fNeedFileInfo: false).GetFrames()?
            .Select(frame => frame.GetMethod())
            .OfType<MethodInfo>()
            .Select(ResolveTestMethod)
            .FirstOrDefault(method => method is not null && HasXunitTestAttribute(method))!;

        return testMethod?.Name ?? AppDomain.CurrentDomain.FriendlyName;
    }

    private static string ResolveProjectPath()
    {
        string? processPath = System.Environment.ProcessPath;
        if (LooksLikeTestHostPath(processPath))
            return processPath!;

        string? commandLineAssembly = System.Environment.GetCommandLineArgs().FirstOrDefault(LooksLikeUserAssemblyPath);
        if (!string.IsNullOrWhiteSpace(commandLineAssembly))
            return commandLineAssembly;

        string? loadedAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic)
            .Select(assembly => assembly.Location)
            .FirstOrDefault(LooksLikeUserAssemblyPath);
        if (!string.IsNullOrWhiteSpace(loadedAssembly))
            return loadedAssembly;

        if (!string.IsNullOrWhiteSpace(processPath))
            return processPath;

        return Assembly.GetEntryAssembly()?.Location
            ?? processPath
            ?? Path.Combine(AppContext.BaseDirectory, AppDomain.CurrentDomain.FriendlyName);
    }

    private static bool LooksLikeUserAssemblyPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            return false;

        string fileName = Path.GetFileName(path);
        if (fileName.Equals("netstandard.dll", StringComparison.OrdinalIgnoreCase))
            return false;

        return !fileName.StartsWith("testhost", StringComparison.OrdinalIgnoreCase)
            && !fileName.StartsWith("xunit", StringComparison.OrdinalIgnoreCase)
            && !fileName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)
            && !fileName.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
            && !fileName.StartsWith("dotnet-", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeTestHostPath(string? path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && Path.GetFileName(path).StartsWith("testhost", StringComparison.OrdinalIgnoreCase);
    }

    private static void PublishNonBlocking(Task task)
    {
        if (task.IsCompletedSuccessfully)
            return;

        _ = ObservePublicationAsync(task);
    }

    private static async Task ObservePublicationAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
        }
    }

    private static MethodInfo? ResolveTestMethod(MethodInfo method)
    {
        if (HasXunitTestAttribute(method))
            return method;

        Type? stateMachineType = method.DeclaringType;
        Type? containingType = stateMachineType?.DeclaringType;
        if (method.Name != nameof(IAsyncStateMachine.MoveNext) || stateMachineType is null || containingType is null)
            return null;

        return containingType.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(candidate => candidate.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType == stateMachineType && HasXunitTestAttribute(candidate));
    }

    private static bool HasXunitTestAttribute(MethodInfo method)
    {
        return method.GetCustomAttributes(inherit: true)
            .Any(attribute =>
            {
                Type attributeType = attribute.GetType();
                string? fullName = attributeType.FullName;
                return fullName is not null
                    && fullName.StartsWith("Xunit.", StringComparison.Ordinal)
                    && (fullName.EndsWith("FactAttribute", StringComparison.Ordinal) || fullName.EndsWith("TheoryAttribute", StringComparison.Ordinal));
            });
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