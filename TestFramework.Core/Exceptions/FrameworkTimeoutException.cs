using System;
using System.Collections.Generic;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when a framework-managed operation exceeds its allowed timeout.
/// </summary>
public class FrameworkTimeoutException : TimelineFrameworkException
{
    /// <summary>
    /// Initializes the exception with a timeout-focused message.
    /// </summary>
    /// <param name="friendlyMessage">The user-facing message.</param>
    /// <param name="innerException">An optional inner exception.</param>
    /// <param name="recoverySteps">Optional recovery guidance.</param>
    /// <param name="availableOptions">Optional related values or options.</param>
    public FrameworkTimeoutException(
        string friendlyMessage,
        Exception? innerException = null,
        IReadOnlyList<string>? recoverySteps = null,
        IReadOnlyList<string>? availableOptions = null)
        : base(
            friendlyMessage,
            recoverySteps ??
            [
                "Increase the timeout or reduce the amount of work performed during this operation.",
                "Inspect logs and dependency readiness signals to find what did not become ready in time."
            ],
            availableOptions,
            innerException)
    {
    }
}