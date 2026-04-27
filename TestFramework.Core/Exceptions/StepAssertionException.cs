using System;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when a step-based fluent assertion fails outside an assertion scope.
/// </summary>
public class StepAssertionException : Exception
{
    /// <summary>
    /// Initializes a new assertion exception with the provided failure message.
    /// </summary>
    /// <param name="message">The assertion failure message.</param>
    public StepAssertionException(string message) : base(message) { }
}
