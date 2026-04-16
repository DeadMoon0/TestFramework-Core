using System;

namespace TestFramework.Core.Exceptions;

public class VariableNeedsToBeImmutableException(string? message = null, Exception? innerException = null) : Exception(message, innerException);