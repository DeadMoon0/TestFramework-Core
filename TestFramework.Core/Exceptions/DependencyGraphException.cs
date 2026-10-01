using System;
using System.Collections.Generic;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when a dependency graph cannot be resolved due to cycles, missing providers, or conflicting ownership.
/// </summary>
public class DependencyGraphException : TimelineFrameworkException
{
    /// <summary>
    /// Initializes the exception with the user-facing message and this type's general recovery advice.
    /// </summary>
    /// <remarks>
    /// Set <see cref="TimelineFrameworkException.RecoverySteps"/> and
    /// <see cref="TimelineFrameworkException.AvailableOptions"/> in an object initializer when the thrower knows more.
    /// </remarks>
    /// <param name="friendlyMessage">The user-facing message.</param>
    internal DependencyGraphException(string friendlyMessage)
        : this(friendlyMessage, null)
    {
    }

    /// <summary>
    /// Initializes the exception with the user-facing message and the failure that caused it.
    /// </summary>
    /// <param name="friendlyMessage">The user-facing message.</param>
    /// <param name="innerException">The underlying cause, or <see langword="null"/>.</param>
    internal DependencyGraphException(string friendlyMessage, Exception? innerException)
        : base(friendlyMessage, DefaultRecoverySteps, [], innerException)
    {
    }

    private static IReadOnlyList<string> DefaultRecoverySteps =>
        [
            "Check the dependency graph for cycles, missing providers, or conflicting ownership.",
            "Reduce ambiguity so each dependency can be resolved to a single valid provider and ordering path."
        ];
}