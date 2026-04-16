using System;

namespace TestFrameworkCore.Exceptions;

public class ValueAssertionException : Exception
{
    public ValueAssertionException(string message) : base(message) { }
}
