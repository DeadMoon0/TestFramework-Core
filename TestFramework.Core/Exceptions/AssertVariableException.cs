using System;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when a variable assertion does not match the expected value or predicate.
/// </summary>
/// <param name="identifier">The variable identifier when the assertion targets a named variable.</param>
/// <param name="value">The actual value that failed the assertion.</param>
public class AssertVariableException(VariableIdentifier? identifier, object? value)
    : AssertionFailedException($"Variable '{identifier?.Identifier ?? "<const>"}' did not hold: its value was {value ?? "null"}.", null)
{
    /// <summary>
    /// The variable asserted on, or <see langword="null"/> for a constant.
    /// </summary>
    public VariableIdentifier? Identifier { get; } = identifier;
}