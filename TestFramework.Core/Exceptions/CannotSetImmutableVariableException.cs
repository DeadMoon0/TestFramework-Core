using System;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Exceptions;

public class CannotSetImmutableVariableException : Exception
{
    public VariableIdentifier? Identifier { get; }

    public CannotSetImmutableVariableException(VariableIdentifier identifier)
        : base($"Variable '{identifier}' is marked as immutable and cannot be set.")
    {
        Identifier = identifier;
    }

    public CannotSetImmutableVariableException(string? message = null, Exception? innerException = null)
        : base(message, innerException) { }
}