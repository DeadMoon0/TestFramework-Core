using System;

namespace TestFrameworkCore.Exceptions;

public class VariableNeedsToBeImmutableException(string? message = null, Exception? innerException = null) : Exception(message, innerException);