using System;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when code attempts to overwrite a variable marked as immutable.
/// </summary>
public class CannotSetImmutableVariableException : Exception
{
    /// <summary>
    /// Gets the identifier of the immutable variable when one is known.
    /// </summary>
    public VariableIdentifier? Identifier { get; }

    /// <summary>
    /// Initializes the exception for a named immutable variable.
    /// </summary>
    public CannotSetImmutableVariableException(VariableIdentifier identifier)
        : base($"Variable '{identifier}' is marked as immutable and cannot be set.")
    {
        Identifier = identifier;
    }

    /// <summary>
    /// Initializes the exception with a custom message.
    /// </summary>
    public CannotSetImmutableVariableException(string? message = null, Exception? innerException = null)
        : base(message, innerException) { }
}