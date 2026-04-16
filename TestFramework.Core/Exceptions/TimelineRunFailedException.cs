using System;
using System.Collections.Generic;
using System.Text;
using TestFramework.Core.Timelines;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Carries the details of every step that did not complete successfully.
/// </summary>
public record FailedStepInfo(string StageName, string StepName, Exception? StepException);

/// <summary>
/// Thrown by <see cref="TimelineRun.EnsureRanToCompletion"/> when one
/// or more timeline steps finished in a non-complete state.
/// The <see cref="FailedSteps"/> list contains the full context for each failure so test output
/// immediately shows which step failed and why.
/// </summary>
public class TimelineRunFailedException : Exception
{
    public IReadOnlyList<FailedStepInfo> FailedSteps { get; }

    public TimelineRunFailedException(IReadOnlyList<FailedStepInfo> failedSteps)
        : base(BuildMessage(failedSteps))
    {
        FailedSteps = failedSteps;
    }

    private static string BuildMessage(IReadOnlyList<FailedStepInfo> failedSteps)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Timeline run failed: {failedSteps.Count} step(s) did not complete.");
        foreach (var f in failedSteps)
        {
            string exInfo = f.StepException is null
                ? "no exception recorded"
                : $"{f.StepException.GetType().Name}: {f.StepException.Message}";
            sb.AppendLine($"  [{f.StageName} / {f.StepName}] {exInfo}");
        }
        return sb.ToString().TrimEnd();
    }
}
