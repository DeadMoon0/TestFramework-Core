using System;
using System.Collections.Generic;
using TestFramework.Core.Steps.Options;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown during pipeline preprocessing when a step declares a typed input but the
/// declared type of the producer's output is not assignable to the required input type.
/// Inherits from TimelineFrameworkException to provide consistent error recovery guidance.
/// </summary>
public class IOContractTypeViolationException(
    string stepName,
    StepIOEntry input,
    Type producerType,
    string? producerStepName,
    bool producerIsExternal)
    : TimelineFrameworkException(
        BuildFriendlyMessage(stepName, input, producerType),
        BuildRecoverySteps(stepName, input, producerType, producerStepName, producerIsExternal),
        BuildAvailableOptions(input, producerType))
{
    /// <summary>
    /// Gets the name of the step with the invalid typed input contract.
    /// </summary>
    public string StepName { get; } = stepName;

    /// <summary>
    /// Gets the input contract entry that was violated.
    /// </summary>
    public StepIOEntry Input { get; } = input;

    /// <summary>
    /// Gets the produced CLR type that failed the assignability check.
    /// </summary>
    public Type ProducerType { get; } = producerType;

    /// <summary>
    /// Gets the name of the step that last declared the conflicting producer type, if the value did not come from an external input.
    /// </summary>
    public string? ProducerStepName { get; } = producerStepName;

    /// <summary>
    /// Gets a value indicating whether the conflicting producer type came from an externally supplied input.
    /// </summary>
    public bool ProducerIsExternal { get; } = producerIsExternal;

    private static string BuildFriendlyMessage(string stepName, StepIOEntry input, Type producerType)
    {
        return $"Step '{stepName}' expects {input.Kind} '{input.Key}' to be of type '{input.DeclaredType?.Name}' but received '{producerType.Name}' which is not compatible.";
    }

    private static IReadOnlyList<string> BuildRecoverySteps(string stepName, StepIOEntry input, Type producerType, string? producerStepName, bool producerIsExternal)
    {
        var steps = new List<string>();

        string producerOrigin = producerIsExternal
            ? "the external input"
            : producerStepName is not null
                ? $"step '{producerStepName}'"
                : "the producing step";

        steps.Add($"Check that {producerOrigin} produces the correct type: {input.DeclaredType?.FullName}");
        steps.Add($"Verify the step produces type {input.DeclaredType?.Name}, not {producerType.Name}");

        if (producerIsExternal)
        {
            steps.Add($"When setting external {input.Kind} '{input.Key}', ensure it is of type {input.DeclaredType?.FullName}");
        }
        else if (producerStepName is not null)
        {
            steps.Add($"Update step '{producerStepName}' to output the correct type or add a Transform() step to convert to {input.DeclaredType?.Name}");
        }

        steps.Add("See ERROR-HANDLING.md for IO contract pattern documentation");
        return steps;
    }

    private static IReadOnlyList<string> BuildAvailableOptions(StepIOEntry input, Type producerType)
    {
        return new List<string>
        {
            $"Expected: {input.DeclaredType?.FullName ?? input.DeclaredType?.Name ?? "unknown"}",
            $"Actual: {producerType.FullName ?? producerType.Name}"
        };
    }
}

