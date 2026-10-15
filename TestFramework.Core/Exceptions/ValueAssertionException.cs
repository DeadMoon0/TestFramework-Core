using System;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when a value-based or variable-based fluent assertion fails outside an assertion scope.
/// </summary>
public class ValueAssertionException : AssertionFailedException
{
    /// <summary>
    /// Initializes a new assertion exception with the provided failure message.
    /// </summary>
    /// <param name="message">The assertion failure message.</param>
    public ValueAssertionException(string message) : base(message, null) { }

    /// <summary>
    /// Initializes a new assertion exception with the failure message and where the assertion was written.
    /// </summary>
    /// <param name="message">The assertion failure message.</param>
    /// <param name="site">Where the assertion was written, or <see langword="null"/>.</param>
    public ValueAssertionException(string message, string? site) : base(message, site) { }
}
