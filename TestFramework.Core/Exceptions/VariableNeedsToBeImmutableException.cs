using System;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when an operation requires an immutable variable binding but a mutable reference was supplied.
/// </summary>
/// <param name="message">The exception message.</param>
/// <param name="innerException">The inner exception.</param>
public class VariableNeedsToBeImmutableException(string? message = null, Exception? innerException = null) : Exception(message, innerException);