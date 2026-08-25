using System;
using System.Collections.Generic;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when a variable holds a different type than the one being read.
/// </summary>
/// <remarks>
/// The cast this replaces threw <c>InvalidCastException</c>, whose message names two types and not the
/// variable - so the reader learned that something somewhere was a string when an int was wanted, and had
/// to go looking for which of the run's variables it was.
/// </remarks>
public class VariableTypeMismatchException : TimelineFrameworkException
{
    /// <summary>
    /// Initializes the exception for a variable read as the wrong type.
    /// </summary>
    /// <param name="identifier">The variable being read.</param>
    /// <param name="expectedType">The type the caller asked for.</param>
    /// <param name="actualType">The type the variable actually holds.</param>
    public VariableTypeMismatchException(VariableIdentifier identifier, Type expectedType, Type actualType)
        : base(
            $"Variable '{identifier.Identifier}' holds '{actualType.Name}', not the requested '{expectedType.Name}'.",
            new[]
            {
                $"Read it as '{actualType.Name}': GetVariable<{actualType.Name}>(\"{identifier.Identifier}\").",
                $"Or set it as '{expectedType.Name}' where it is written, if that is what it was meant to be.",
                "Check whether the identifier points at a different variable than the one you intended.",
            },
            new List<string>
            {
                $"Requested type: {expectedType.FullName}",
                $"Actual type: {actualType.FullName}",
            })
    {
        this.VariableName = identifier.Identifier;
        this.ExpectedType = expectedType;
        this.ActualType = actualType;
    }

    /// <summary>Gets the variable that was read.</summary>
    public string VariableName { get; }

    /// <summary>Gets the type the caller asked for.</summary>
    public Type ExpectedType { get; }

    /// <summary>Gets the type the variable actually holds.</summary>
    public Type ActualType { get; }
}
