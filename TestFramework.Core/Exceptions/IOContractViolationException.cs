using System;
using TestFramework.Core.Steps.Options;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown during pipeline preprocessing when a step declares a required input
/// that is not produced by any earlier step and was not provided externally.
/// </summary>
public class IOContractViolationException(string stepName, StepIOEntry input)
    : Exception($"Step '{stepName}' declares a required {input.Kind} input '{input.Key}' " +
                $"but no prior step produces it and it was not provided externally.")
{
    /// <summary>
    /// Gets the name of the step with the missing input dependency.
    /// </summary>
    public string StepName { get; } = stepName;

    /// <summary>
    /// Gets the missing input contract entry.
    /// </summary>
    public StepIOEntry Input { get; } = input;
}
