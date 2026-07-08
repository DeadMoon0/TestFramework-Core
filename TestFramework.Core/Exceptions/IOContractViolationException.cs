using System;
using System.Collections.Generic;
using System.Linq;
using TestFramework.Core.Steps.Options;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown during pipeline preprocessing when a step declares a required input
/// that is not produced by any earlier step and was not provided externally.
/// Inherits from TimelineFrameworkException to provide consistent error recovery guidance.
/// </summary>
public class IOContractViolationException(
    string stepName,
    StepIOEntry input,
    int stepIndex,
    IReadOnlyList<string> precedingStepNames,
    IReadOnlyList<string> availableKeys,
    IReadOnlyList<string> similarKeys)
    : TimelineFrameworkException(
        BuildFriendlyMessage(stepName, input, stepIndex),
        BuildRecoverySteps(input, stepName, precedingStepNames),
        BuildAvailableOptions(availableKeys, similarKeys))
{
    /// <summary>
    /// Gets the name of the step with the missing input dependency.
    /// </summary>
    public string StepName { get; } = stepName;

    /// <summary>
    /// Gets the missing input contract entry.
    /// </summary>
    public StepIOEntry Input { get; } = input;

    /// <summary>
    /// Gets the zero-based main-stage step index where the violation was detected.
    /// </summary>
    public int StepIndex { get; } = stepIndex;

    /// <summary>
    /// Gets the names of the steps that were already processed before the violation occurred.
    /// </summary>
    public IReadOnlyList<string> PrecedingStepNames { get; } = precedingStepNames;

    /// <summary>
    /// Gets the known keys of the same kind that were available when validation failed.
    /// </summary>
    public IReadOnlyList<string> AvailableKeys { get; } = availableKeys;

    /// <summary>
    /// Gets the small set of available keys that most closely resemble the missing key.
    /// </summary>
    public IReadOnlyList<string> SimilarKeys { get; } = similarKeys;

    private static string BuildFriendlyMessage(string stepName, StepIOEntry input, int stepIndex)
    {
        return $"Step '{stepName}' (index {stepIndex}) requires {input.Kind} '{input.Key}' but it was not produced by earlier steps and not provided externally.";
    }

    private static IReadOnlyList<string> BuildRecoverySteps(StepIOEntry input, string stepName, IReadOnlyList<string> precedingStepNames)
    {
        var steps = new List<string>();

        if (input.Kind.ToString() == "Variable")
        {
            steps.Add($"Check that a prior step sets '{input.Key}' as output or add it to external variables");
            steps.Add($"Verify variable name spelling: expected '{input.Key}'");
            if (precedingStepNames.Count == 0)
            {
                steps.Add("Add a SetVariable() step before this step to initialize the required variable");
            }
        }
        else if (input.Kind.ToString() == "Artifact")
        {
            steps.Add($"Check that a prior step registers artifact '{input.Key}' or provide it externally");
            steps.Add($"Verify artifact name spelling: expected '{input.Key}'");
            steps.Add("Use RegisterArtifact() or SetupArtifact() in an earlier step");
        }

        steps.Add("See ERROR-HANDLING.md for IO contract pattern documentation");
        return steps;
    }

    private static IReadOnlyList<string> BuildAvailableOptions(IReadOnlyList<string> availableKeys, IReadOnlyList<string> similarKeys)
    {
        var options = new List<string>();

        if (availableKeys.Count > 0)
        {
            options.Add($"Available: {string.Join(", ", availableKeys.OrderBy(k => k))}");
        }
        else
        {
            options.Add("Available: (none)");
        }

        if (similarKeys.Count > 0)
        {
            options.Add($"Similar: {string.Join(", ", similarKeys.OrderBy(k => k))}");
        }

        return options;
    }
}
