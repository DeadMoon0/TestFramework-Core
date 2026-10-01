using System;
using System.Collections.Generic;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when framework configuration or builder input is missing, conflicting, or otherwise invalid.
/// </summary>
public class FrameworkConfigurationException : TimelineFrameworkException
{
    /// <summary>
    /// Initializes the exception with the user-facing message and this type's general recovery advice.
    /// </summary>
    /// <remarks>
    /// Set <see cref="TimelineFrameworkException.RecoverySteps"/> and
    /// <see cref="TimelineFrameworkException.AvailableOptions"/> in an object initializer when the thrower knows more.
    /// </remarks>
    /// <param name="friendlyMessage">The user-facing message.</param>
    public FrameworkConfigurationException(string friendlyMessage)
        : this(friendlyMessage, null)
    {
    }

    /// <summary>
    /// Initializes the exception with the user-facing message and the failure that caused it.
    /// </summary>
    /// <param name="friendlyMessage">The user-facing message.</param>
    /// <param name="innerException">The underlying cause, or <see langword="null"/>.</param>
    public FrameworkConfigurationException(string friendlyMessage, Exception? innerException)
        : base(friendlyMessage, DefaultRecoverySteps, [], innerException)
    {
    }

    private static IReadOnlyList<string> DefaultRecoverySteps =>
        [
            "Check the surrounding configuration or builder selections.",
            "Verify required identifiers, names, connection settings, or dependency declarations are present and non-conflicting."
        ];
}