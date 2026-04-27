using System;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when a value-based or variable-based fluent assertion fails outside an assertion scope.
/// </summary>
public class ValueAssertionException : Exception
{
    /// <summary>
    /// Initializes a new assertion exception with the provided failure message.
    /// </summary>
    /// <param name="message">The assertion failure message.</param>
    public ValueAssertionException(string message) : base(message) { }
}
