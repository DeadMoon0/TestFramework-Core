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

internal class CoreRunner
{
    internal async Task RunStage(StageInstance instance, IServiceProvider serviceProvider, ScopedLogger logger, VariableStore variableStore, ArtifactStore artifactStore, DebuggingRunSession debuggingSession)
    {
        StageExecutionPlanner executionPlanner = new StageExecutionPlanner(instance, artifactStore);

        foreach (var layer in executionPlanner.BuildLayers())
        {
            await ExecuteLayerAsync(instance.Stage.Name, layer, serviceProvider, logger, variableStore, artifactStore, debuggingSession);

            if (LayerFailed(layer))
            {
                instance.Result.State = StageState.Error;
                return;
            }
        }

        instance.Result.State = StageState.Complete;
    }

    private static Task ExecuteLayerAsync(string stageName, IReadOnlyList<StageExecutionPlanner.ScheduledStep> layer, IServiceProvider serviceProvider, ScopedLogger logger, VariableStore variableStore, ArtifactStore artifactStore, DebuggingRunSession debuggingSession)
    {
        return Task.WhenAll(layer.Select(x => ExecuteStepAsync(stageName, x.Index, x.Step, serviceProvider, logger, variableStore, artifactStore, debuggingSession)));
    }

    private static bool LayerFailed(IEnumerable<StageExecutionPlanner.ScheduledStep> layer)
    {
        return layer.Any(x => x.Step.State != StepState.Complete);
    }

    private static async Task ExecuteStepAsync(string stageName, int stepIndex, StepInstanceGeneric step, IServiceProvider serviceProvider, ScopedLogger logger, VariableStore variableStore, ArtifactStore artifactStore, DebuggingRunSession debuggingSession)
    {
        using var _ = debuggingSession.BeginStepExecutionContext(stageName, stepIndex);
        await debuggingSession.WaitWhenBreakpointHit(stageName, stepIndex);

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

            StepResultGeneric stepResult = await ExecuteAttemptAsync(step, serviceProvider, logger, variableStore, artifactStore);
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
            await debuggingSession.TransitionStepAsync(
                stageName,
                stepIndex,
                willRetry ? DebugLifecycleState.WaitingForRetry : outcomeState,
                DebugLifecycleState.Running,
                outcomeState);
        }
        while (willRetry);

        step.Freeze();
    }

    private static async Task DelayForRetryAsync(StepInstanceGeneric step, int iteration, VariableStore variableStore)
    {
        await Task.Delay(step.Step.RetryOptions.CalcDelay.GetValue(variableStore)?.Invoke(iteration) ?? throw new ArgumentNullException(nameof(step.Step.RetryOptions.CalcDelay), "RetryOptions.CalcDelay cannot be null."));
    }

    private static async Task<StepResultGeneric> ExecuteAttemptAsync(StepInstanceGeneric step, IServiceProvider serviceProvider, ScopedLogger logger, VariableStore variableStore, ArtifactStore artifactStore)
    {
        var stopwatch = Stopwatch.StartNew();
        StepResultGeneric stepResult = new StepResultGeneric();
        TimeSpan timeout = step.Step.TimeOutOptions.TimeOut.GetValue(variableStore);

        // One source of truth for the timeout. There used to be two — a CTS *and* a WaitAsync(timeout)
        // — and on the WaitAsync path the CTS was disposed without ever being cancelled, so the step
        // was never told to stop, its exception went unobserved, and the retry started immediately
        // alongside the attempt it was supposedly replacing.
        CancellationTokenSource cancellationTokenSource = new(timeout);
        bool ownsCancellationTokenSource = true;
        Task<object?>? executionTask = null;

        try
        {
            executionTask = step.Step.ExecuteGeneric(serviceProvider, variableStore, artifactStore, logger, cancellationTokenSource.Token);
            object? result = await executionTask.WaitAsync(cancellationTokenSource.Token);
            stepResult.Result = result;
            stepResult.State = StepState.Complete;

            ApplyResultBindings(step, result, variableStore);
        }
        catch (TimeoutException exception)
        {
            stepResult.Exception = exception;
            stepResult.State = StepState.Timeout;
        }
        catch (OperationCanceledException exception) when (cancellationTokenSource.IsCancellationRequested)
        {
            stepResult.Exception = new TimeoutException($"Step '{step.Step.Name}' timed out after {timeout}.", exception);
            stepResult.State = StepState.Timeout;

            if (executionTask is not null && !executionTask.IsCompleted)
            {
                logger.LogWarning($"Step '{step.Step.Name}' did not stop when it timed out after {timeout}. The abandoned attempt may still be running and writing to this run's stores.");

                // Hand ownership of the CTS to the continuation: disposing it here would race the
                // still-running attempt, and someone has to observe that attempt's exception.
                ownsCancellationTokenSource = false;
                ObserveAbandonedAttempt(executionTask, cancellationTokenSource);
            }
        }
        catch (Exception exception)
        {
            stepResult.Exception = exception;
            stepResult.State = step.Step.ErrorHandlingOptions.IgnoreExceptionTypes.Any(x => x.IsAssignableFrom(exception.GetType()))
                ? StepState.Complete
                : StepState.Error;
        }
        finally
        {
            if (ownsCancellationTokenSource)
                cancellationTokenSource.Dispose();
        }

        stopwatch.Stop();
        stepResult.TimeSpent = stopwatch.Elapsed;
        stepResult.Freeze();
        return stepResult;
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