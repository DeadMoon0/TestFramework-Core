using System;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Exceptions;

public class VariableDoesNotExistException(VariableIdentifier identifier) : Exception("The Variable you try to Read from does not Exist. Variable: " + identifier)
{
    public VariableIdentifier Identifier { get; } = identifier;
}