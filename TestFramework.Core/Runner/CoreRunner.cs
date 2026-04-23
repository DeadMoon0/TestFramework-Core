using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Logging;
using TestFramework.Core.Stages;
using TestFramework.Core.Steps;
using TestFramework.Core.Variables;
using TestFramework.Core.Logging.BuildInEvents;

namespace TestFramework.Core.Runner;

internal class CoreRunner
{
    internal async Task RunStage(StageInstance instance, IServiceProvider serviceProvider, ScopedLogger logger, VariableStore variableStore, ArtifactStore artifactStore, DebuggingRunSession debuggingSession)
    {
        int i = 0;
        foreach (StepInstanceGeneric step in instance.Steps)
        {
            logger.LogInformation("");
            logger.Log(new EnterStepLogEvent(step));
            using var _ = logger.EnterIndentScope();

            await debuggingSession.EnterStepAsync(i);
            await debuggingSession.WaitWhenBreakpointHit(instance.Stage.Name, i);

            int iteration = 0;
            do
            {
                iteration++;

                if (iteration > 1)
                {
                    logger.Log(new EnterStepIterationLogEvent(iteration));
                    await Task.Delay(step.Step.RetryOptions.CalcDelay.GetValue(variableStore)?.Invoke(iteration) ?? throw new ArgumentNullException(nameof(step.Step.RetryOptions.CalcDelay), "RetryOptions.CalcDelay cannot be null."));
                }

                var stopwatch = Stopwatch.StartNew();
                StepResultGeneric stepResult = new StepResultGeneric();
                TimeSpan timeout = step.Step.TimeOutOptions.TimeOut.GetValue(variableStore);
                using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(timeout);
                try
                {
                    Task<object?> executionTask = step.Step.ExecuteGeneric(serviceProvider, variableStore, artifactStore, logger, cancellationTokenSource.Token);
                    var r = await executionTask.WaitAsync(timeout);
                    stepResult.Result = r;
                    stepResult.State = StepState.Complete;
                    if (step.Step.DoesReturn) variableStore.SetVariable("out", r);
                }
                catch (TimeoutException e)
                {
                    stepResult.Exception = e;
                    stepResult.State = StepState.Timeout;
                }
                catch (OperationCanceledException e) when (cancellationTokenSource.IsCancellationRequested)
                {
                    stepResult.Exception = new TimeoutException($"Step '{step.Step.Name}' timed out after {timeout}.", e);
                    stepResult.State = StepState.Timeout;
                }
                catch (Exception e)
                {
                    stepResult.Exception = e;
                    if (step.Step.ErrorHandlingOptions.IgnoreExceptionTypes.Any(x => x.IsAssignableFrom(e.GetType()))) stepResult.State = StepState.Complete;
                    else stepResult.State = StepState.Error;
                }
                stopwatch.Stop();
                stepResult.Freeze();
                step.RetryResults.Add(stepResult);
                logger.Log(new StepResultLogEvent(step.Step.Name, step.Step.LabelOptions.Label, stepResult, stopwatch.Elapsed));
                await debuggingSession.SetStepResultAsync(stepResult);
            } while (step.Step.RetryOptions.MaxRetryCount.GetValue(variableStore) >= iteration && step.RetryResults.Last().State != StepState.Complete);
            step.Freeze();

            if (step.State != StepState.Complete)
            {
                instance.Result.State = StageState.Error;
                return;
            }
            i++;
        }
        instance.Result.State = StageState.Complete;
    }
}