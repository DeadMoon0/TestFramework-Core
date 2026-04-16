using System;
using TestFrameworkCore.Variables;

namespace TestFrameworkCore.Exceptions;

public class AssertVariableException(VariableIdentifier? identifier, object? value) : Exception("The Variable-Value (" + (identifier ?? "<Const>") + ") was not as expected. Value: " + value)
{
}