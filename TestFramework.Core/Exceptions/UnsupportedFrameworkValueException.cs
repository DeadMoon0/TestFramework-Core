using System;
using System.Collections.Generic;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when a framework operation receives or reaches an unsupported mode, type, or value.
/// </summary>
public class UnsupportedFrameworkValueException : TimelineFrameworkException
{
    /// <summary>
    /// Initializes the exception with an unsupported-value message.
    /// </summary>
    /// <param name="friendlyMessage">The user-facing message.</param>
    /// <param name="recoverySteps">Optional recovery guidance.</param>
    /// <param name="availableOptions">Optional related values or options.</param>
    /// <param name="innerException">An optional inner exception.</param>
    public UnsupportedFrameworkValueException(
        string friendlyMessage,
        IReadOnlyList<string>? recoverySteps = null,
        IReadOnlyList<string>? availableOptions = null,
        Exception? innerException = null)
        : base(
            friendlyMessage,
            recoverySteps ??
            [
                "Use one of the supported values or modes for this API.",
                "Check the current package version for the supported options and contract."
            ],
            availableOptions,
            innerException)
    {
    }
}