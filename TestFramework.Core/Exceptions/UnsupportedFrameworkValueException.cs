using System;
using System.Collections.Generic;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when a framework operation receives or reaches an unsupported mode, type, or value.
/// </summary>
public class UnsupportedFrameworkValueException : TimelineFrameworkException
{
    /// <summary>
    /// Initializes the exception with the user-facing message and this type's general recovery advice.
    /// </summary>
    /// <remarks>
    /// Set <see cref="TimelineFrameworkException.RecoverySteps"/> and
    /// <see cref="TimelineFrameworkException.AvailableOptions"/> in an object initializer when the thrower knows more.
    /// </remarks>
    /// <param name="friendlyMessage">The user-facing message.</param>
    public UnsupportedFrameworkValueException(string friendlyMessage)
        : this(friendlyMessage, null)
    {
    }

    /// <summary>
    /// Initializes the exception with the user-facing message and the failure that caused it.
    /// </summary>
    /// <param name="friendlyMessage">The user-facing message.</param>
    /// <param name="innerException">The underlying cause, or <see langword="null"/>.</param>
    public UnsupportedFrameworkValueException(string friendlyMessage, Exception? innerException)
        : base(friendlyMessage, DefaultRecoverySteps, [], innerException)
    {
    }

    private static IReadOnlyList<string> DefaultRecoverySteps =>
        [
            "Use one of the supported values or modes for this API.",
            "Check the current package version for the supported options and contract."
        ];
}