using System;

namespace TestFramework.Core.Exceptions;

public class ValueAssertionException : Exception
{
    public ValueAssertionException(string message) : base(message) { }
}
