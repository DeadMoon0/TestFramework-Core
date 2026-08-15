using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Debugger;

internal class DebuggingRunSession
{
    /// <summary>
    /// Bound chosen so a burst of logging never grows without limit, while a producer only ever waits
    /// once the consumer is thousands of signals behind.
    /// </summary>
    private const int SignalQueueCapacity = 8192;

    private readonly AsyncLocal<ExecutionContextInfo?> currentExecutionContext = new();
    private readonly AsyncLocal<IterationContextInfo?> currentIterationContext = new();

    /// <summary>
    /// Every signal goes through this queue and is delivered by a single consumer, in the order it
    /// was produced. Ordering is load-bearing: OutputRunDebugger correlates log entries against the
    /// step and iteration that are currently active, and silently drops entries that do not match.
    /// </summary>
    private readonly Channel<SignalWorkItem> signalQueue = Channel.CreateBounded<SignalWorkItem>(
        new BoundedChannelOptions(SignalQueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

    private readonly object drainStartGate = new();
    private Task? drainTask;
    private int writerCompleted;
    private bool sessionInitialized;

    internal DebuggingRunSession(IRunDebugger debugger)
    {
        Debugger = debugger;
    }

    internal string SessionId { get; } = Guid.NewGuid().ToString();
    internal IRunDebugger Debugger { get; }

    /// <summary>
    /// Gets a value indicating whether anything downstream will use the signals this session emits.
    /// Callers gate expensive signal preparation on this.
    /// </summary>
    internal bool IsCapturing => Debugger.IsCapturing;

    internal Task InitSessionAsync(TimelineRunStructure runStructure)
    {
        // Naming the run walks the whole stack and resolving the project path scans loaded assemblies.
        // Neither is cheap, and neither has a reader when nothing is capturing.
        if (!IsCapturing)
        {
            sessionInitialized = true;
            return Task.CompletedTask;
        }

        string testName = GetTestName();
        string projectPath = GetProjectPath();
        sessionInitialized = true;

        return EnqueueAndAwait(() => Debugger.SignalInitTimelineRunAsync(SessionId, testName, projectPath, runStructure));
    }

    // Transitions are queued, not awaited. The queue is FIFO with a single reader, so ordering
    // against log entries is already guaranteed by enqueueing - waiting for the drain to catch up
    // would only add a cross-thread round trip to the run's hot path, once per step. That cost is
    // invisible on a developer machine and very visible on a two-core CI runner, where the drain
    // competes for the thread pool with every other test running in parallel.
    // FinishSessionAsync is what guarantees everything has actually been delivered.

    internal Task TransitionRunAsync(DebugLifecycleState state, DebugLifecycleState? previousState = null)
    {
        Enqueue(() => Debugger.SignalEntityTransitionAsync(SessionId, DebugEntityKind.Run, null, null, state, previousState));
        return Task.CompletedTask;
    }

    internal Task TransitionStageAsync(string stage, DebugLifecycleState state, DebugLifecycleState? previousState = null)
    {
        Enqueue(() => Debugger.SignalEntityTransitionAsync(SessionId, DebugEntityKind.Stage, stage, null, state, previousState));
        return Task.CompletedTask;
    }

    internal Task TransitionStepAsync(string stage, int stepId, DebugLifecycleState state, DebugLifecycleState? previousState = null, DebugLifecycleState? outcomeState = null)
    {
        Enqueue(() => Debugger.SignalEntityTransitionAsync(SessionId, DebugEntityKind.Step, stage, stepId, state, previousState, outcomeState));
        return Task.CompletedTask;
    }

    internal void PublishVariableUpdate(VariableIdentifier identifier, VariableState state)
    {
        if (!sessionInitialized)
            return;

        string? stage = currentExecutionContext.Value?.Stage;
        int? stepId = currentExecutionContext.Value?.StepId;
        Enqueue(() => Debugger.SignalValueUpdateAsync(SessionId, identifier, DebugValueKind.Variable, stage, stepId, state.Envelope));
    }

    internal void PublishArtifactUpdate(ArtifactIdentifier identifier, ArtifactState state)
    {
        if (!sessionInitialized)
            return;

        string? stage = currentExecutionContext.Value?.Stage;
        int? stepId = currentExecutionContext.Value?.StepId;
        Enqueue(() => Debugger.SignalValueUpdateAsync(SessionId, identifier, DebugValueKind.Artifact, stage, stepId, state.Envelope));
    }

    /// <summary>
    /// Queues a log entry for delivery. The caller is not blocked, but the entry keeps its place in
    /// the stream relative to the step transitions around it.
    /// </summary>
    internal void PublishLog(DebugLogEntry entry)
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

        Enqueue(() => Debugger.SignalLogEntryAsync(SessionId, entryWithContext));
    }

    /// <summary>
    /// Queues an assertion result for delivery, in order with the logs around it.
    /// </summary>
    internal void PublishAssertion(DebugAssertionEntry entry)
    {
        DebugAssertionEntry entryWithTimestamp = new()
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
        };

        Enqueue(() => Debugger.SignalAssertionAsync(SessionId, entryWithTimestamp));
    }

    /// <summary>
    /// Delivers the finish signal and waits until the queue has drained, so everything the run
    /// produced has reached its debuggers before the test method returns.
    /// </summary>
    internal async Task FinishSessionAsync()
    {
        Task finished = EnqueueAndAwait(() => Debugger.SignalTimelineRunFinishedAsync(SessionId));

        Interlocked.Exchange(ref writerCompleted, 1);
        signalQueue.Writer.TryComplete();

        Task? drain = Volatile.Read(ref drainTask);
        if (drain is not null)
            await drain.ConfigureAwait(false);

        await finished.ConfigureAwait(false);
    }

    /// <summary>
    /// Asks the debugger whether this step should pause, and waits for the answer.
    /// </summary>
    /// <remarks>
    /// Deliberately NOT queued. This is control flow, not telemetry: every step calls it before it
    /// starts, so routing it through the signal queue would make a step's start wait on the queue
    /// draining, coupling the run's progress to logging throughput. On a machine with few cores
    /// that turns into seconds of delay per step.
    /// </remarks>
    internal Task WaitWhenBreakpointHit(string stage, int index)
    {
        return Debugger.SignalAndWaitBreakpointHitAsync(SessionId, stage, index);
    }

    private void Enqueue(Func<Task> work) => Enqueue(new SignalWorkItem(work, null));

    private Task EnqueueAndAwait(Func<Task> work)
    {
        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Enqueue(new SignalWorkItem(work, completion));
        return completion.Task;
    }

    private void Enqueue(SignalWorkItem item)
    {
        bool queued = false;

        try
        {
            if (Volatile.Read(ref writerCompleted) == 0)
            {
                queued = signalQueue.Writer.TryWrite(item);
                if (!queued)
                {
                    // The queue is full, so the consumer is thousands of signals behind. Blocking the
                    // producer here is what the bound is for, and it keeps this thread's signals in
                    // order — an async fallback could let a later signal overtake this one.
                    signalQueue.Writer.WriteAsync(item).AsTask().GetAwaiter().GetResult();
                    queued = true;
                }
            }
        }
        catch (ChannelClosedException)
        {
            // The session finished while this signal was in flight; there is nowhere left to send it.
        }

        if (queued)
        {
            EnsureDraining();
            return;
        }

        item.Completion?.TrySetResult(true);
    }

    private void EnsureDraining()
    {
        if (Volatile.Read(ref drainTask) is not null)
            return;

        lock (drainStartGate)
        {
            if (drainTask is null)
                Volatile.Write(ref drainTask, Task.Run(DrainAsync));
        }
    }

    private async Task DrainAsync()
    {
        await foreach (SignalWorkItem item in signalQueue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                await item.Work().ConfigureAwait(false);
                item.Completion?.TrySetResult(true);
            }
            catch (Exception exception)
            {
                if (item.Completion is null)
                    Debug.WriteLine(exception);
                else
                    item.Completion.TrySetException(exception);
            }
        }
    }

    private sealed record SignalWorkItem(Func<Task> Work, TaskCompletionSource<bool>? Completion);

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