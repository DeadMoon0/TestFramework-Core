using System;
using System.Collections.Generic;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when framework configuration or builder input is missing, conflicting, or otherwise invalid.
/// </summary>
public class FrameworkConfigurationException : TimelineFrameworkException
{
    /// <summary>
    /// Initializes the exception with a configuration-focused message.
    /// </summary>
    /// <param name="friendlyMessage">The user-facing message.</param>
    /// <param name="recoverySteps">Optional recovery guidance.</param>
    /// <param name="availableOptions">Optional related values or options.</param>
    /// <param name="innerException">An optional inner exception.</param>
    public FrameworkConfigurationException(
        string friendlyMessage,
        IReadOnlyList<string>? recoverySteps = null,
        IReadOnlyList<string>? availableOptions = null,
        Exception? innerException = null)
        : base(
            friendlyMessage,
            recoverySteps ??
            [
                "Check the surrounding configuration or builder selections.",
                "Verify required identifiers, names, connection settings, or dependency declarations are present and non-conflicting."
            ],
            availableOptions,
            innerException)
    {
    }
}