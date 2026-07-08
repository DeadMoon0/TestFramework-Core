using System;
using System.Collections.Generic;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when a dependency graph cannot be resolved due to cycles, missing providers, or conflicting ownership.
/// </summary>
public class DependencyGraphException : TimelineFrameworkException
{
    /// <summary>
    /// Initializes the exception with a dependency-graph message.
    /// </summary>
    /// <param name="friendlyMessage">The user-facing message.</param>
    /// <param name="recoverySteps">Optional recovery guidance.</param>
    /// <param name="availableOptions">Optional related values or options.</param>
    /// <param name="innerException">An optional inner exception.</param>
    public DependencyGraphException(
        string friendlyMessage,
        IReadOnlyList<string>? recoverySteps = null,
        IReadOnlyList<string>? availableOptions = null,
        Exception? innerException = null)
        : base(
            friendlyMessage,
            recoverySteps ??
            [
                "Check the dependency graph for cycles, missing providers, or conflicting ownership.",
                "Reduce ambiguity so each dependency can be resolved to a single valid provider and ordering path."
            ],
            availableOptions,
            innerException)
    {
    }
}