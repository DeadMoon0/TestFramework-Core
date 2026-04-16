using System;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Exceptions;

public class AssertVariableException(VariableIdentifier? identifier, object? value) : Exception("The Variable-Value (" + (identifier ?? "<Const>") + ") was not as expected. Value: " + value)
{
}