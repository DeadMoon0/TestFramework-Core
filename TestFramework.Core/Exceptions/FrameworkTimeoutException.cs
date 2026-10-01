using System;
using System.Collections.Generic;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when a framework-managed operation exceeds its allowed timeout.
/// </summary>
public class FrameworkTimeoutException : TimelineFrameworkException
{
    /// <summary>
    /// Initializes the exception with the user-facing message and this type's general recovery advice.
    /// </summary>
    /// <remarks>
    /// Set <see cref="TimelineFrameworkException.RecoverySteps"/> and
    /// <see cref="TimelineFrameworkException.AvailableOptions"/> in an object initializer when the thrower knows more.
    /// </remarks>
    /// <param name="friendlyMessage">The user-facing message.</param>
    public FrameworkTimeoutException(string friendlyMessage)
        : this(friendlyMessage, null)
    {
    }

    /// <summary>
    /// Initializes the exception with the user-facing message and the failure that caused it.
    /// </summary>
    /// <param name="friendlyMessage">The user-facing message.</param>
    /// <param name="innerException">The underlying cause, or <see langword="null"/>.</param>
    public FrameworkTimeoutException(string friendlyMessage, Exception? innerException)
        : base(friendlyMessage, DefaultRecoverySteps, [], innerException)
    {
    }

    private static IReadOnlyList<string> DefaultRecoverySteps =>
        [
            "Increase the timeout or reduce the amount of work performed during this operation.",
            "Inspect logs and dependency readiness signals to find what did not become ready in time."
        ];
}