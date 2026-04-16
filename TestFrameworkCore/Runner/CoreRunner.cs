using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFrameworkCore.Artifacts;
using TestFrameworkCore.Debugger;
using TestFrameworkCore.Logging;
using TestFrameworkCore.Logging.BuildInEvents;
using TestFrameworkCore.Stages;
using TestFrameworkCore.Steps;
using TestFrameworkCore.Variables;

namespace TestFrameworkCore.Runner;

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
                CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(step.Step.TimeOutOptions.TimeOut.GetValue(variableStore));
                try
                {
                    var r = await step.Step.ExecuteGeneric(serviceProvider, variableStore, artifactStore, logger, cancellationTokenSource.Token);
                    stepResult.Result = r;
                    stepResult.State = StepState.Complete;
                    if (step.Step.DoesReturn) variableStore.SetVariable("out", r);
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