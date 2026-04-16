using System;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Exceptions;

public class VariableDoesNotYetExistException(VariableIdentifier identifier) : Exception("The Variable you try to Read from does not yet Exist. Variable: " + identifier)
{
    public VariableIdentifier Identifier { get; } = identifier;
}
