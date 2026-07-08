using System;
using System.Collections.Generic;
using System.Linq;
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
    /// <summary>
    /// Gets the collection of failed steps captured by the exception.
    /// </summary>
    public IReadOnlyList<FailedStepInfo> FailedSteps { get; }

    /// <summary>
    /// Initializes the exception from the captured failed steps.
    /// </summary>
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
            sb.AppendLine($"  [{f.StageName} / {f.StepName}]");

            if (f.StepException is null)
            {
                sb.AppendLine("    no exception recorded");
                continue;
            }

            if (f.StepException is TimelineFrameworkException frameworkException)
            {
                foreach (string line in frameworkException.ToString().Split(System.Environment.NewLine).Select(x => "    " + x))
                    sb.AppendLine(line);
                continue;
            }

            sb.AppendLine($"    {f.StepException.GetType().Name}: {f.StepException.Message}");
        }
        return sb.ToString().TrimEnd();
    }
}
