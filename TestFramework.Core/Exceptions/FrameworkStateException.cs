using System;
using System.Collections.Generic;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when a framework operation is invalid for the current runtime or builder state.
/// </summary>
public class FrameworkStateException : TimelineFrameworkException
{
    /// <summary>
    /// Initializes the exception with the user-facing message and this type's general recovery advice.
    /// </summary>
    /// <remarks>
    /// Set <see cref="TimelineFrameworkException.RecoverySteps"/> and
    /// <see cref="TimelineFrameworkException.AvailableOptions"/> in an object initializer when the thrower knows more.
    /// </remarks>
    /// <param name="friendlyMessage">The user-facing message.</param>
    public FrameworkStateException(string friendlyMessage)
        : this(friendlyMessage, null)
    {
    }

    /// <summary>
    /// Initializes the exception with the user-facing message and the failure that caused it.
    /// </summary>
    /// <param name="friendlyMessage">The user-facing message.</param>
    /// <param name="innerException">The underlying cause, or <see langword="null"/>.</param>
    public FrameworkStateException(string friendlyMessage, Exception? innerException)
        : base(friendlyMessage, DefaultRecoverySteps, [], innerException)
    {
    }

    private static IReadOnlyList<string> DefaultRecoverySteps =>
        [
            "Check the preceding setup, build, or initialization steps.",
            "Verify the requested operation is valid for the current runtime state before retrying."
        ];
}