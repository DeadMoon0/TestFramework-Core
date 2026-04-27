using System;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when a variable is requested but no such variable exists.
/// </summary>
/// <param name="identifier">The missing variable identifier.</param>
public class VariableDoesNotExistException(VariableIdentifier identifier) : Exception("The Variable you try to Read from does not Exist. Variable: " + identifier)
{
    /// <summary>
    /// Gets the missing variable identifier.
    /// </summary>
    public VariableIdentifier Identifier { get; } = identifier;
}