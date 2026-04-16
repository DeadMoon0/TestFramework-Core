using System;

namespace TestFrameworkCore.Exceptions;

public class StepAssertionException : Exception
{
    public StepAssertionException(string message) : base(message) { }
}
