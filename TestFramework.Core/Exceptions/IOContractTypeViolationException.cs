using System;
using System.Text;
using TestFramework.Core.Steps.Options;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown during pipeline preprocessing when a step declares a typed input but the
/// declared type of the producer's output is not assignable to the required input type.
/// </summary>
public class IOContractTypeViolationException(
    string stepName,
    StepIOEntry input,
    Type producerType,
    string? producerStepName,
    bool producerIsExternal)
    : Exception(CreateMessage(stepName, input, producerType, producerStepName, producerIsExternal))
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

    private static string CreateMessage(string stepName, StepIOEntry input, Type producerType, string? producerStepName, bool producerIsExternal)
    {
        string producerOrigin = producerIsExternal
            ? "an external input"
            : producerStepName is not null
                ? $"step '{producerStepName}'"
                : "an earlier step";

        StringBuilder builder = new();
        builder.AppendLine($"Step '{stepName}' declares a {input.Kind} input '{input.Key}' of type '{input.DeclaredType?.Name}', but {producerOrigin} declared '{producerType.Name}', which is not assignable to the required input type.");
        builder.Append($"Expected assignable to: {input.DeclaredType?.FullName ?? input.DeclaredType?.Name ?? "<unknown>"}. Actual producer type: {producerType.FullName ?? producerType.Name}.");
        return builder.ToString();
    }
}
