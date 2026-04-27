using System;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when a variable is requested before it has been created.
/// </summary>
/// <param name="identifier">The variable identifier that is not yet available.</param>
public class VariableDoesNotYetExistException(VariableIdentifier identifier) : Exception("The Variable you try to Read from does not yet Exist. Variable: " + identifier)
{
    /// <summary>
    /// Gets the variable identifier that is not yet available.
    /// </summary>
    public VariableIdentifier Identifier { get; } = identifier;
}
