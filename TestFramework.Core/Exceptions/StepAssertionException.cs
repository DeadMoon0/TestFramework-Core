using System;

namespace TestFramework.Core.Exceptions;

public class StepAssertionException : Exception
{
    public StepAssertionException(string message) : base(message) { }
}
