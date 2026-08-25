using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Logging;
using TestFramework.Core.Stages;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Steps.SystemSteps;
using TestFramework.Core.Variables;
using TestFramework.Core.Logging.BuildInEvents;

namespace TestFramework.Core.Runner;

/// <summary>
/// Runs one stage's steps: schedules them, gives each attempt its deadline and its licence to write, and
/// tells the run's observers what happened.
/// </summary>
/// <param name="observers">
/// What is watching this run's steps. Told about every attempt, and never able to change one - see
/// <see cref="StepObservers"/>.
/// </param>
internal class CoreRunner(StepObservers observers)
{
    internal async Task RunStage(StageInstance instance, IServiceProvider serviceProvider, ScopedLogger logger, VariableStore variableStore, ArtifactStore artifactStore, DebuggingRunSession debuggingSession)
    {
        StageExecutionPlanner executionPlanner = new StageExecutionPlanner(instance, artifactStore);

        foreach (var layer in executionPlanner.BuildLayers())
        {
            await ExecuteLayerAsync(instance.Stage.Name, layer, serviceProvider, logger, variableStore, artifactStore, debuggingSession, instance.Stage.IsCleanupStage);

            if (LayerFailed(layer))
            {
                instance.Result.State = StageState.Error;
                return;
            }
        }

        instance.Result.State = StageState.Complete;
    }

    private Task ExecuteLayerAsync(string stageName, IReadOnlyList<StageExecutionPlanner.ScheduledStep> layer, IServiceProvider serviceProvider, ScopedLogger logger, VariableStore variableStore, ArtifactStore artifactStore, DebuggingRunSession debuggingSession, bool isCleanupStage)
    {
        return Task.WhenAll(layer.Select(x => ExecuteStepAsync(stageName, x.Index, x.Step, serviceProvider, logger, variableStore, artifactStore, debuggingSession, isCleanupStage)));
    }

    private static bool LayerFailed(IEnumerable<StageExecutionPlanner.ScheduledStep> layer)
    {
        return layer.Any(x => x.Step.State != StepState.Complete);
    }

    private async Task ExecuteStepAsync(string stageName, int stepIndex, StepInstanceGeneric step, IServiceProvider serviceProvider, ScopedLogger logger, VariableStore variableStore, ArtifactStore artifactStore, DebuggingRunSession debuggingSession, bool isCleanupStage)
    {
        using var _ = debuggingSession.BeginStepExecutionContext(stageName, stepIndex);

        // Checked per step, not only per stage: a run cancelled midway through a stage must not keep
        // working through the rest of that stage's steps.
        // Teardown is exempt: skipping cleanup steps because the run was cancelled would strand
        // exactly the resources cancellation exists to release.
        if (debuggingSession.IsCancellationRequested && !isCleanupStage)
        {
            // A skipped step still records an outcome. StepInstance.State reads the last retry
            // result, so returning without one turns "this step did not run" into an exception when
            // anything later inspects the run.
            step.RetryResults.Add(new StepResultGeneric { State = StepState.Skipped });
            step.Freeze();

            await debuggingSession.TransitionStepAsync(stageName, stepIndex, DebugLifecycleState.Skipped, DebugLifecycleState.Initialized);
            return;
        }

        await debuggingSession.WaitWhenBreakpointHit(stageName, stepIndex);

        // One gate per step, not per run: beginning an attempt abandons the previous one, which is what a
        // retry needs and exactly what a step running beside this one must not suffer.
        StepAttemptGate attemptGate = new StepAttemptGate();
        string label = LabelOf(step);

        int iteration = 0;
        bool willRetry;
        do
        {
            iteration++;

            await debuggingSession.TransitionStepAsync(stageName, stepIndex, DebugLifecycleState.Running, iteration == 1 ? DebugLifecycleState.Initialized : DebugLifecycleState.WaitingForRetry);

            using var iterationScope = debuggingSession.BeginStepIterationContext(iteration);
            logger.Log(new EnterStepLogEvent(step, iteration));

            if (iteration > 1)
                await DelayForRetryAsync(step, iteration, variableStore);

            AttemptScope scope = new AttemptScope(
                attemptGate,
                attemptGate.Begin(label, iteration),
                new StepObservation(label, step.Step.Name, stageName, iteration));

            observers.Starting(scope.Observation, logger);

            StepResultGeneric stepResult = await ExecuteAttemptAsync(step, serviceProvider, logger, variableStore, artifactStore, scope, debuggingSession.RunCancellationToken);
            step.RetryResults.Add(stepResult);

            willRetry = ShouldRetry(step, variableStore, iteration, stepResult);
            logger.Log(new StepResultLogEvent(
                step.Step.Name,
                step.Step.LabelOptions.Label,
                stepResult,
                stepResult.TimeSpent,
                iteration,
                willRetry,
                step.Step.ResultOptions.ResultBindings));
            DebugLifecycleState outcomeState = MapLifecycleState(stepResult.State);

            // The exception is only in scope here. Anywhere downstream it exists solely as rendered
            // log text, which is why a debugger could previously say a step went red but not why.
            // A result that carries an exception yet is not in an error state was absorbed by
            // ErrorHandlingOptions.IgnoreExceptionTypes.
            DebugFailureDetail? failure = DebugFailureDetail.Capture(
                stepResult.Exception,
                iteration,
                willRetry,
                wasSuppressed: stepResult.Exception is not null && stepResult.State is not (StepState.Error or StepState.Timeout));

            await debuggingSession.TransitionStepAsync(
                stageName,
                stepIndex,
                willRetry ? DebugLifecycleState.WaitingForRetry : outcomeState,
                DebugLifecycleState.Running,
                outcomeState,
                failure);
        }
        while (willRetry);

        step.Freeze();
    }

    /// <summary>
    /// What to call this step in an attempt's warnings and in what observers are told.
    /// </summary>
    /// <remarks>
    /// The label a person chose, else the step's name. A step with neither still has to be nameable, or
    /// beginning its attempt would throw and a nameless step would take the run down with it.
    /// </remarks>
    /// <param name="step">The step.</param>
    /// <returns>The label.</returns>
    private static string LabelOf(StepInstanceGeneric step)
    {
        if (!string.IsNullOrWhiteSpace(step.Step.LabelOptions.Label))
            return step.Step.LabelOptions.Label!;

        return string.IsNullOrWhiteSpace(step.Step.Name) ? "unnamed step" : step.Step.Name;
    }

    /// <summary>
    /// One attempt at one step: who is writing, what may still be written, and what observers are told.
    /// </summary>
    /// <remarks>
    /// Travels together because it is answers to one question - which attempt is this - and passing the
    /// three separately is how they drift apart. It folds into <c>RunContext</c> when steps take one.
    /// </remarks>
    private sealed record AttemptScope(StepAttemptGate Gate, StepAttempt Attempt, StepObservation Observation);

    private static async Task DelayForRetryAsync(StepInstanceGeneric step, int iteration, VariableStore variableStore)
    {
        await Task.Delay(step.Step.RetryOptions.CalcDelay.GetValue(variableStore)?.Invoke(iteration) ?? throw new ArgumentNullException(nameof(step.Step.RetryOptions.CalcDelay), "RetryOptions.CalcDelay cannot be null."));
    }

    private async Task<StepResultGeneric> ExecuteAttemptAsync(StepInstanceGeneric step, IServiceProvider serviceProvider, ScopedLogger logger, VariableStore variableStore, ArtifactStore artifactStore, AttemptScope scope, CancellationToken runCancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        StepResultGeneric stepResult = new StepResultGeneric();
        TimeSpan timeout = step.Step.TimeOutOptions.TimeOut.GetValue(variableStore);

        // What the step writes through: the same stores, but able to say which attempt is writing, so an
        // attempt the run has stopped waiting for cannot reach the next test.
        VariableStore attemptVariables = variableStore.ForAttempt(scope.Gate, scope.Attempt);
        ArtifactStore attemptArtifacts = artifactStore.ForAttempt(scope.Gate, scope.Attempt);

        // One source of truth for the timeout. There used to be two — a CTS *and* a WaitAsync(timeout)
        // — and on the WaitAsync path the CTS was disposed without ever being cancelled, so the step
        // was never told to stop, its exception went unobserved, and the retry started immediately
        // alongside the attempt it was supposedly replacing.
        CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(runCancellationToken);
        cancellationTokenSource.CancelAfter(timeout);
        bool ownsCancellationTokenSource = true;
        Task<object?>? executionTask = null;

        try
        {
            executionTask = step.Step.ExecuteGeneric(serviceProvider, attemptVariables, attemptArtifacts, logger, cancellationTokenSource.Token);
            object? result = await executionTask.WaitAsync(cancellationTokenSource.Token);
            stepResult.Result = result;
            stepResult.State = StepState.Complete;

            // The run's own store, not the attempt's: binding a result is the runner recording what the
            // step returned, and it happens once the attempt is over.
            ApplyResultBindings(step, result, variableStore);
        }
        catch (TimeoutException exception)
        {
            stepResult.Exception = exception;
            stepResult.State = StepState.Timeout;
            observers.TimedOut(scope.Observation, logger);
        }
        catch (OperationCanceledException exception) when (cancellationTokenSource.IsCancellationRequested)
        {
            stepResult.State = StepState.Timeout;

            // The step has been told to stop; now it gets a short while to say what it was doing. A step
            // that answers within the grace window has its own account surfaced, which is the whole
            // reason packages used to under-cut their deadlines by hand.
            Exception? own = executionTask is null
                ? null
                : await AwaitGraceAsync(executionTask, StepDeadline.GraceFor(timeout));

            stepResult.Exception = own ?? new TimeoutException($"Step '{step.Step.Name}' timed out after {timeout}.", exception);

            if (executionTask is not null && !executionTask.IsCompleted)
            {
                logger.LogWarning($"Step '{step.Step.Name}' did not stop when it timed out after {timeout}. Its writes to this run's stores are refused from here on.");

                // Hand ownership of the CTS to the continuation: disposing it here would race the
                // still-running attempt, and someone has to observe that attempt's exception.
                ownsCancellationTokenSource = false;
                ObserveAbandonedAttempt(executionTask, cancellationTokenSource);
            }

            observers.TimedOut(scope.Observation, logger);
        }
        catch (Exception exception)
        {
            stepResult.Exception = exception;
            stepResult.State = step.Step.ErrorHandlingOptions.IgnoreExceptionTypes.Any(x => x.IsAssignableFrom(exception.GetType()))
                ? StepState.Complete
                : StepState.Error;

            // Only a real failure. An exception the step was told to ignore did not fail the step, and
            // gathering evidence for every swallowed exception would bury the failures worth looking at.
            if (stepResult.State == StepState.Error)
                observers.Failed(scope.Observation, exception, logger);
        }
        finally
        {
            // The attempt is over, whether it answered, failed or was abandoned. Ending it here is what
            // stops a step that returned - and left something running behind it - from writing on.
            scope.Gate.End(scope.Attempt);

            if (ownsCancellationTokenSource)
                cancellationTokenSource.Dispose();
        }

        stopwatch.Stop();
        stepResult.TimeSpent = stopwatch.Elapsed;
        stepResult.Freeze();
        return stepResult;
    }

    /// <summary>
    /// Waits a little for a cancelled attempt to finish complaining.
    /// </summary>
    /// <remarks>
    /// A step that stops cooperatively usually throws something far more useful than "it timed out" - the
    /// address it was waiting on, the element it could not find. Without this window that exception was
    /// raised into a task nobody was waiting for any more, so the reader got the generic message and the
    /// step's own account was lost. Anything the step throws here wins; a cancellation is not an account
    /// of anything, so it does not.
    /// </remarks>
    /// <param name="executionTask">The attempt.</param>
    /// <param name="grace">How long to wait.</param>
    /// <returns>What the step said, or null when it said nothing in time.</returns>
    private static async Task<Exception?> AwaitGraceAsync(Task executionTask, TimeSpan grace)
    {
        if (grace <= TimeSpan.Zero)
            return null;

        // Decided by which task finished, never by what was thrown: a step's own account is very often a
        // TimeoutException itself, so using the wait's own timeout exception as the signal made the two
        // indistinguishable and lost exactly the message this window exists to keep.
        Task finished = await Task.WhenAny(executionTask, Task.Delay(grace, CancellationToken.None));

        if (!ReferenceEquals(finished, executionTask))
            return null;

        // A cancellation is not an account of anything; only a real failure is worth surfacing.
        return executionTask.Exception?.InnerExceptions.FirstOrDefault(static inner => inner is not OperationCanceledException);
    }

    /// <summary>
    /// Keeps an abandoned step attempt from surfacing later as an unobserved task exception, and
    /// releases its cancellation source once the attempt finally settles.
    /// </summary>
    private static void ObserveAbandonedAttempt(Task executionTask, CancellationTokenSource cancellationTokenSource)
    {
        _ = executionTask.ContinueWith(
            static (task, state) =>
            {
                if (task.Exception is not null)
                    Debug.WriteLine(task.Exception);

                ((CancellationTokenSource)state!).Dispose();
            },
            cancellationTokenSource,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static void ApplyResultBindings(StepInstanceGeneric step, object? result, VariableStore variableStore)
    {
        if (!step.Step.DoesReturn)
            return;

        foreach (ResultBinding binding in step.Step.ResultOptions.ResultBindings)
            variableStore.SetVariable(binding.Variable, binding.Accessor(result));
    }

    private static bool ShouldRetry(StepInstanceGeneric step, VariableStore variableStore, int iteration, StepResultGeneric stepResult)
    {
        return step.Step.RetryOptions.MaxRetryCount.GetValue(variableStore) >= iteration && stepResult.State != StepState.Complete;
    }

    private static DebugLifecycleState MapLifecycleState(StepState state)
    {
        return state switch
        {
            StepState.NotRun => DebugLifecycleState.Initialized,
            StepState.Complete => DebugLifecycleState.Complete,
            StepState.Timeout => DebugLifecycleState.Timeout,
            StepState.Error => DebugLifecycleState.Error,
            StepState.Skipped => DebugLifecycleState.Skipped,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };
    }
}