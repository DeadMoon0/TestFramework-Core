using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TestFramework.Core.Timelines;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Carries the details of every step that did not complete successfully.
/// </summary>
public record FailedStepInfo(string StageName, string StepName, Exception? StepException)
{
    /// <summary>
    /// Why the step never ran, when it was skipped rather than failed; <see langword="null"/> for a step that
    /// ran and did not complete.
    /// </summary>
    public string? SkipReason { get; init; }
}

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

        // The failures first, each in full, and the steps they kept from running after them, once and grouped
        // by why: those belong in the report, but a pile of them on top would bury the one failure worth reading.
        foreach (var f in failedSteps.Where(static f => f.SkipReason is null))
        {
            sb.AppendLine($"  [{f.StageName} / {f.StepName}]");

            if (f.StepException is null)
            {
                sb.AppendLine("    no exception recorded");
                continue;
            }

            // In full, whatever its type: a step's own exception is as likely as the framework's to carry its
            // real cause one level down, and naming only its type and message lost it.
            foreach (string line in f.StepException.ToString().Split(System.Environment.NewLine).Select(x => "    " + x))
                sb.AppendLine(line);
        }
        List<FailedStepInfo> skipped = [.. failedSteps.Where(static f => f.SkipReason is not null)];
        if (skipped.Count > 0)
        {
            sb.AppendLine($"  {skipped.Count} step(s) did not run:");
            foreach (IGrouping<string?, FailedStepInfo> reason in skipped.GroupBy(static f => f.SkipReason))
            {
                sb.AppendLine($"    {reason.Key}");
                foreach (FailedStepInfo f in reason)
                    sb.AppendLine($"      [{f.StageName} / {f.StepName}]");
            }
        }

        return sb.ToString().TrimEnd();
    }
}
