using System;
using System.Collections.Generic;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when a framework operation is invalid for the current runtime or builder state.
/// </summary>
public class FrameworkStateException : TimelineFrameworkException
{
    /// <summary>
    /// Initializes the exception with a state-focused message.
    /// </summary>
    /// <param name="friendlyMessage">The user-facing message.</param>
    /// <param name="recoverySteps">Optional recovery guidance.</param>
    /// <param name="availableOptions">Optional related values or options.</param>
    /// <param name="innerException">An optional inner exception.</param>
    public FrameworkStateException(
        string friendlyMessage,
        IReadOnlyList<string>? recoverySteps = null,
        IReadOnlyList<string>? availableOptions = null,
        Exception? innerException = null)
        : base(
            friendlyMessage,
            recoverySteps ??
            [
                "Check the preceding setup, build, or initialization steps.",
                "Verify the requested operation is valid for the current runtime state before retrying."
            ],
            availableOptions,
            innerException)
    {
    }
}