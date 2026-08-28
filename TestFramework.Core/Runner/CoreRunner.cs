using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Exceptions;
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
/// <param name="values">
/// Where this run's resources ended up. Handed to every step, so asking is one call rather than each
/// package's own way of finding out.
/// </param>
internal class CoreRunner(StepObservers observers, ValueResolution values)
{
    internal async Task RunStage(StageInstance instance, IServiceProvider serviceProvider, ScopedLogger logger, VariableStore variableStore, ArtifactStore artifactStore, DebuggingRunSession debuggingSession)
    {
        StageExecutionPlanner executionPlanner = new StageExecutionPlanner(instance, artifactStore);
        bool anyLayerFailed = false;

        foreach (var layer in executionPlanner.BuildLayers())
        {
            await ExecuteLayerAsync(instance.Stage.Name, layer, serviceProvider, logger, variableStore, artifactStore, debuggingSession, instance.Stage.IsCleanupStage);

            if (!LayerFailed(layer))
                continue;

            // A failed layer ends an ordinary stage - its later layers depend on what just did not happen.
            // It must not end the cleanup stage: the layers after a failed cleanup step are the run's own
            // teardown - deconstructing artifacts, taking the environment down - and skipping those because
            // a user's cleanup step went red is how one red step leaks somebody's database rows and a fleet
            // of containers, silently.
            if (!instance.Stage.IsCleanupStage)
            {
                instance.Result.State = StageState.Error;
                return;
            }

            anyLayerFailed = true;
        }

        instance.Result.State = anyLayerFailed ? StageState.Error : StageState.Complete;
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

            // Teardown must not inherit the run's stop signal: after a consumer stops a run, the cleanup
            // stage is precisely what still has to happen, and a cleanup step handed the fired token would
            // self-cancel before deconstructing anything - reaching teardown in name only.
            CancellationToken stopToken = isCleanupStage ? CancellationToken.None : debuggingSession.RunCancellationToken;

            if (iteration > 1 && !await TryDelayForRetryAsync(step, iteration, stageName, stepIndex, variableStore, logger, debuggingSession, stopToken))
            {
                step.Freeze();
                return;
            }

            AttemptScope scope = new AttemptScope(
                attemptGate,
                attemptGate.Begin(label, iteration),
                new StepObservation(label, step.Step.Name, stageName, iteration));

            await observers.StartingAsync(
                scope.Observation,
                () => EvidenceContext(serviceProvider, logger, variableStore, artifactStore, stopToken),
                logger);

            StepResultGeneric stepResult = await ExecuteAttemptAsync(step, serviceProvider, logger, variableStore, artifactStore, scope, debuggingSession, stopToken);
            step.RetryResults.Add(stepResult);

            // A cancelled run gets no further attempts: retrying against a fired token is a string of
            // instant no-ops that reads like the step failing repeatedly. Teardown keeps its retries.
            willRetry = ShouldRetry(step, variableStore, iteration, stepResult)
                && (isCleanupStage || !debuggingSession.IsCancellationRequested);
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

    /// <summary>
    /// The run as an observer sees it: the same services, stores, logger and resolved values, no attempt,
    /// and a budget of its own.
    /// </summary>
    /// <remarks>
    /// The run's stores rather than the attempt's, because gathering evidence is the run recording what
    /// happened and not the step's last act - an attempt the runner has stopped waiting for has lost its
    /// licence to write, and the screenshot of that very failure is the last thing that should be dropped
    /// with it. The budget is <see cref="StepObservers.EvidenceBudget"/> rather than what the step had,
    /// which by definition is gone.
    /// </remarks>
    private RunContext EvidenceContext(
        IServiceProvider serviceProvider,
        ScopedLogger logger,
        VariableStore variableStore,
        ArtifactStore artifactStore,
        CancellationToken runCancellationToken)
        => RunContext.Ambient(
            serviceProvider,
            variableStore,
            artifactStore,
            logger,
            values,
            runCancellationToken,
            StepObservers.EvidenceBudget);

    /// <summary>
    /// Waits out the retry backoff, or says why the retry is not happening.
    /// </summary>
    /// <remarks>
    /// Two exits besides the normal one, and neither is allowed to crash the run. A backoff cut short by
    /// the run being stopped simply ends the wait - the next attempt then reports the stop honestly. A
    /// <c>CalcDelay</c> that throws (or resolves to null) used to escape the runner entirely, which aborted
    /// the run with no teardown and no run object; it now records an error result on the step, so the run
    /// fails with the author's exception where the author can see it.
    /// </remarks>
    /// <returns>True to proceed with the retry attempt; false when the step is finished.</returns>
    private static async Task<bool> TryDelayForRetryAsync(
        StepInstanceGeneric step,
        int iteration,
        string stageName,
        int stepIndex,
        VariableStore variableStore,
        ScopedLogger logger,
        DebuggingRunSession debuggingSession,
        CancellationToken stopToken)
    {
        try
        {
            TimeSpan delay = step.Step.RetryOptions.CalcDelay.GetValue(variableStore)?.Invoke(iteration)
                ?? throw new FrameworkConfigurationException(
                    $"Step '{step.Step.Name}' cannot retry: RetryOptions.CalcDelay resolved to null.",
                    ["Set CalcDelay to a function of the attempt number, or leave the default in place."],
                    []);

            await Task.Delay(delay, stopToken);

            return true;
        }
        catch (OperationCanceledException) when (stopToken.IsCancellationRequested)
        {
            // The run was stopped mid-backoff. The attempt still runs, so the step's record says it was
            // stopped rather than trailing off in a WaitingForRetry state nothing ever leaves.
            return true;
        }
        catch (Exception exception)
        {
            logger.LogError("Step '{0}' could not compute its retry delay, so it keeps its last result and does not retry.\n{1}", step.Step.Name, exception.ToString());

            StepResultGeneric refused = new StepResultGeneric { State = StepState.Error, Exception = exception };
            refused.Freeze();
            step.RetryResults.Add(refused);

            await debuggingSession.TransitionStepAsync(
                stageName,
                stepIndex,
                DebugLifecycleState.Error,
                DebugLifecycleState.WaitingForRetry,
                DebugLifecycleState.Error,
                DebugFailureDetail.Capture(exception, iteration, willRetry: false, wasSuppressed: false));

            return false;
        }
    }

    private async Task<StepResultGeneric> ExecuteAttemptAsync(StepInstanceGeneric step, IServiceProvider serviceProvider, ScopedLogger logger, VariableStore variableStore, ArtifactStore artifactStore, AttemptScope scope, DebuggingRunSession debuggingSession, CancellationToken stopToken)
    {
        var stopwatch = Stopwatch.StartNew();
        StepResultGeneric stepResult = new StepResultGeneric();

        // What an observer is handed, built only if one is watching. Deliberately not the attempt's
        // context: an observer photographing a step that just died must not have its own writes thrown
        // away with that attempt, and must not inherit the budget that just ran out.
        RunContext Evidence() => EvidenceContext(serviceProvider, logger, variableStore, artifactStore, stopToken);

        // Read and refused here rather than trusted: the timeout may come from a variable, so plan time
        // cannot see it, and this used to be the last unguarded line before the try - a timeout variable
        // that was never set, or a zero or negative value, escaped the runner entirely and aborted the
        // run with no teardown. Zero is refused rather than read as "no deadline" because it would arm a
        // token that fires instantly under a deadline reporting unbounded - the exact disagreement
        // StepDeadline exists to prevent.
        TimeSpan timeout;

        try
        {
            timeout = step.Step.TimeOutOptions.TimeOut.GetValue(variableStore);

            if (timeout != Timeout.InfiniteTimeSpan && timeout <= TimeSpan.Zero)
            {
                throw new FrameworkConfigurationException(
                    $"Step '{step.Step.Name}' has a timeout of {timeout}, which no step can run under.",
                    ["State a positive timeout, or Timeout.InfiniteTimeSpan for a step with no deadline."],
                    []);
            }
        }
        catch (Exception exception)
        {
            stepResult.Exception = exception;
            stepResult.State = StepState.Error;
            await observers.FailedAsync(scope.Observation, exception, Evidence, logger);

            scope.Gate.End(scope.Attempt);
            stopwatch.Stop();
            stepResult.TimeSpent = stopwatch.Elapsed;
            stepResult.Freeze();
            return stepResult;
        }

        // What the step writes through: the same stores, but able to say which attempt is writing, so an
        // attempt the run has stopped waiting for cannot reach the next test.
        VariableStore attemptVariables = variableStore.ForAttempt(scope.Gate, scope.Attempt);
        ArtifactStore attemptArtifacts = artifactStore.ForAttempt(scope.Gate, scope.Attempt);

        // One source of truth for the timeout. There used to be two — a CTS *and* a WaitAsync(timeout)
        // — and on the WaitAsync path the CTS was disposed without ever being cancelled, so the step
        // was never told to stop, its exception went unobserved, and the retry started immediately
        // alongside the attempt it was supposedly replacing.
        CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stopToken);
        // The deadline is built before the cancellation is armed, so the moment it says the time is up is
        // never later than the moment the token fires. The other order leaves a step cancelled while its
        // own deadline still reports time remaining - which is exactly the disagreement this type exists
        // to prevent, and it only shows up under load.
        StepDeadline deadline = new StepDeadline(timeout, cancellationTokenSource.Token);

        cancellationTokenSource.CancelAfter(timeout);
        bool ownsCancellationTokenSource = true;
        Task<object?>? executionTask = null;

        try
        {
            // Built here and nowhere else: the deadline is the same token the step is cancelled with, so a
            // step cannot be told it has time it does not have.
            RunContext context = new RunContext(
                serviceProvider,
                attemptVariables,
                attemptArtifacts,
                logger,
                values,
                deadline,
                scope.Attempt);

            executionTask = step.Step.ExecuteGeneric(context);
            object? result = await executionTask.WaitAsync(cancellationTokenSource.Token);
            stepResult.Result = result;
            stepResult.State = StepState.Complete;

            // The run's own store, not the attempt's: binding a result is the runner recording what the
            // step returned, and it happens once the attempt is over.
            ApplyResultBindings(step, result, variableStore);
        }
        catch (TimeoutException exception) when (deadline.HasExpired)
        {
            // Guarded on the deadline, not the exception type alone: a TimeoutException thrown while the
            // step still had time is a dependency's timeout - an HTTP client's, a driver's - and calling
            // that a step timeout misfiles the failure and books the wrong evidence hook. It lands in the
            // ordinary failure path below instead.
            stepResult.Exception = exception;
            stepResult.State = StepState.Timeout;
            await observers.TimedOutAsync(scope.Observation, Evidence, logger);
        }
        catch (OperationCanceledException exception) when (cancellationTokenSource.IsCancellationRequested)
        {
            // Two very different things arrive here through one token: the step's own deadline firing,
            // and a consumer stopping the whole run. The deadline is built before the cancellation is
            // armed, so at a genuine timeout HasExpired is already true - which makes "not expired" a
            // reliable reading of "the run was stopped", and the frozen record can say what actually
            // happened instead of claiming a timeout that never occurred.
            bool runStopped = !deadline.HasExpired && stopToken.IsCancellationRequested;

            // The step has been told to stop; now it gets a short while to say what it was doing. A step
            // that answers within the grace window has its own account surfaced, which is the whole
            // reason packages used to under-cut their deadlines by hand.
            TimeSpan grace = StepDeadline.GraceFor(timeout);
            GraceOutcome graced = executionTask is null
                ? new GraceOutcome(true, null)
                : await AwaitGraceAsync(executionTask, grace);

            if (executionTask is not null && !executionTask.IsCompleted)
            {
                logger.LogWarning($"Step '{step.Step.Name}' did not stop when it was cancelled. Its writes to this run's stores are refused from here on.");

                // Hand ownership of the CTS to the continuation: disposing it here would race the
                // still-running attempt, and someone has to observe that attempt's exception.
                ownsCancellationTokenSource = false;
                ObserveAbandonedAttempt(executionTask, cancellationTokenSource);
            }

            if (runStopped)
            {
                stepResult.State = StepState.Error;
                stepResult.Exception = graced.Own ?? new OperationCanceledException(
                    $"Step '{step.Step.Name}' was stopped because the run was cancelled{ReasonSuffix(debuggingSession)}.",
                    exception);

                await observers.FailedAsync(scope.Observation, stepResult.Exception, Evidence, logger);
            }
            else
            {
                stepResult.State = StepState.Timeout;

                // When the step never stopped, the generic sentence is true and useless: it reports the
                // timeout and hides that a better explanation existed and was lost. Saying so turns a silent
                // loss into the one thing a reader can act on, and it is the only way this stays closed -
                // "remember to bound every await" is a rule nothing enforces, whereas a failure that names
                // its own cause needs nobody to remember anything.
                stepResult.Exception = graced.Own ?? new TimeoutException(
                    graced.Stopped
                        ? $"Step '{step.Step.Name}' timed out after {timeout}."
                        : $"Step '{step.Step.Name}' timed out after {timeout} and was still running {grace} later, so whatever it "
                            + "was going to say about the failure was never heard. A step keeps its own message by stopping inside "
                            + "that window; the usual cause is an awaited call that does not take the step's deadline.",
                    exception);

                await observers.TimedOutAsync(scope.Observation, Evidence, logger);
            }
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
                await observers.FailedAsync(scope.Observation, exception, Evidence, logger);
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
    /// <returns>Whether the attempt stopped, and what it said if it said anything.</returns>
    private static async Task<GraceOutcome> AwaitGraceAsync(Task executionTask, TimeSpan grace)
    {
        if (grace <= TimeSpan.Zero)
            return new GraceOutcome(executionTask.IsCompleted, null);

        // Decided by which task finished, never by what was thrown: a step's own account is very often a
        // TimeoutException itself, so using the wait's own timeout exception as the signal made the two
        // indistinguishable and lost exactly the message this window exists to keep.
        Task finished = await Task.WhenAny(executionTask, Task.Delay(grace, CancellationToken.None));

        if (!ReferenceEquals(finished, executionTask))
            return new GraceOutcome(false, null);

        // A cancellation is not an account of anything; only a real failure is worth surfacing.
        return new GraceOutcome(
            true,
            executionTask.Exception?.InnerExceptions.FirstOrDefault(static inner => inner is not OperationCanceledException));
    }

    /// <summary>
    /// What the grace window found: whether the attempt stopped, and anything it said on the way out.
    /// </summary>
    /// <remarks>
    /// Both answers come from one observation on purpose. Reading "did it stop" separately afterwards is a
    /// race the message loses: an attempt that finishes in the moment between the two reads is reported as
    /// still running, which is the opposite of what happened.
    /// </remarks>
    /// <param name="Stopped">Whether the attempt finished inside the window.</param>
    /// <param name="Own">What the step threw, when it threw something that was not a cancellation.</param>
    private readonly record struct GraceOutcome(bool Stopped, Exception? Own);

    /// <summary>
    /// The cancellation reason, when the consumer gave one, formatted for the step's own record.
    /// </summary>
    private static string ReasonSuffix(DebuggingRunSession debuggingSession)
        => debuggingSession.CancellationReason is { Length: > 0 } reason ? $" ({reason})" : string.Empty;

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