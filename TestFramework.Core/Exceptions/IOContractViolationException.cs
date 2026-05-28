using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TestFramework.Core.Steps.Options;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown during pipeline preprocessing when a step declares a required input
/// that is not produced by any earlier step and was not provided externally.
/// </summary>
public class IOContractViolationException(
    string stepName,
    StepIOEntry input,
    int stepIndex,
    IReadOnlyList<string> precedingStepNames,
    IReadOnlyList<string> availableKeys,
    IReadOnlyList<string> similarKeys)
    : Exception(CreateMessage(stepName, input, stepIndex, precedingStepNames, availableKeys, similarKeys))
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

    private static string CreateMessage(
        string stepName,
        StepIOEntry input,
        int stepIndex,
        IReadOnlyList<string> precedingStepNames,
        IReadOnlyList<string> availableKeys,
        IReadOnlyList<string> similarKeys)
    {
        StringBuilder builder = new();
        builder.AppendLine($"Step '{stepName}' (main stage index {stepIndex}) declares a required {input.Kind} input '{input.Key}', but nothing earlier in the run produced it and it was not provided externally.");

        if (precedingStepNames.Count > 0)
            builder.AppendLine($"Earlier steps: {string.Join(", ", precedingStepNames)}");
        else
            builder.AppendLine("Earlier steps: none");

        if (availableKeys.Count > 0)
            builder.AppendLine($"Known {input.Kind} keys at this point: {string.Join(", ", availableKeys)}");
        else
            builder.AppendLine($"Known {input.Kind} keys at this point: none");

        if (similarKeys.Count > 0)
            builder.AppendLine($"Similar keys: {string.Join(", ", similarKeys)}");

        builder.Append("Check the producing step order, the key spelling, or whether the missing dependency should be supplied externally.");
        return builder.ToString();
    }
}
