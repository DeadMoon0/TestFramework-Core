using System.Collections.Generic;
using System.Linq;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when a labeled step lookup cannot find any matching step instances.
/// </summary>
public class StepLabelNotFoundException : TimelineFrameworkException
{
    /// <summary>
    /// Initializes the exception for a step label that was not found.
    /// </summary>
    /// <param name="label">The missing step label.</param>
    /// <param name="availableLabels">The labeled steps that were available in the run.</param>
    public StepLabelNotFoundException(string label, IReadOnlyList<string> availableLabels)
        : base(
            $"No step with label '{label}' was found.",
            new[]
            {
                $"Call .Name(\"{label}\") on the step you want to inspect.",
                "Use Step(label) only when exactly one step should match.",
                "Use Steps(label) when the label can appear multiple times."
            },
            availableLabels.Any()
                ? availableLabels.Distinct(System.StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray()
                : new[] { "No labeled steps were recorded for this run." })
    {
    }
}