using System.Collections.Generic;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when a single-step label lookup matches more than one step instance.
/// </summary>
public class StepLabelAmbiguousException : TimelineFrameworkException
{
    /// <summary>
    /// Initializes the exception for a label that matched more than one step instance.
    /// </summary>
    /// <param name="label">The label requested by the caller.</param>
    /// <param name="matchCount">The number of step instances that matched the label.</param>
    public StepLabelAmbiguousException(string label, int matchCount)
        : base(
            $"Label '{label}' matched {matchCount} step instances.",
            new[]
            {
                $"Use Steps(\"{label}\") to retrieve all matching step instances.",
                "Give repeated steps distinct .Name(...) values when you need single-step lookup.",
                "Check whether the label is being reused inside a ForEach or repeated composition block."
            },
            new List<string>
            {
                $"Requested label: {label}",
                $"Matches found: {matchCount}"
            })
    {
    }
}