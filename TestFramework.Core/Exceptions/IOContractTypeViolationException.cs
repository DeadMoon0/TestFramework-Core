using System;
using TestFramework.Core.Steps.Options;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown during pipeline preprocessing when a step declares a typed input but the
/// declared type of the producer's output is not assignable to the required input type.
/// </summary>
public class IOContractTypeViolationException(string stepName, StepIOEntry input, Type producerType)
    : Exception($"Step '{stepName}' declares a {input.Kind} input '{input.Key}' " +
                $"of type '{input.DeclaredType?.Name}' but the producer declared type '{producerType.Name}', " +
                $"which is not assignable to the required input type.")
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
}
