using System;
using TestFrameworkCore.Variables;

namespace TestFrameworkCore.Exceptions;

public class VariableDoesNotYetExistException(VariableIdentifier identifier) : Exception("The Variable you try to Read from does not yet Exist. Variable: " + identifier)
{
    public VariableIdentifier Identifier { get; } = identifier;
}
