using System;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when code attempts to overwrite a variable marked as immutable.
/// </summary>
public class CannotSetImmutableVariableException : TimelineFrameworkException
{
    /// <summary>
    /// Gets the identifier of the immutable variable.
    /// </summary>
    public VariableIdentifier Identifier { get; }

    /// <summary>
    /// Initializes the exception for a named immutable variable.
    /// </summary>
    internal CannotSetImmutableVariableException(VariableIdentifier identifier)
        : base(
            $"Variable '{identifier}' is marked as immutable and cannot be set.",
            [$"Write the new value under another identifier, or read '{identifier}' with Var.Ref instead of Var.RefImmutable."])
    {
        Identifier = identifier;
    }
}