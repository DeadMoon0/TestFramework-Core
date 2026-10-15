using System;
using System.Collections.Generic;
using System.Linq;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when an <see cref="Timelines.Assertions.AssertionScope"/> collects multiple assertion failures.
/// </summary>
public class MultipleAssertionsFailedException : AssertionFailedException
{
    /// <summary>
    /// Gets the individual failure messages recorded in the assertion scope.
    /// </summary>
    public IReadOnlyList<string> Failures { get; }

    /// <summary>
    /// Initializes a new exception that aggregates multiple assertion failures.
    /// </summary>
    /// <param name="failures">The failure messages recorded by the assertion scope.</param>
    public MultipleAssertionsFailedException(IReadOnlyList<string> failures)
        : this(failures, [.. failures.Select(static _ => (string?)null)])
    {
    }

    /// <summary>
    /// Initializes the exception with where each failed assertion was written.
    /// </summary>
    /// <param name="failures">The failure messages recorded by the assertion scope.</param>
    /// <param name="sites">Where each was written, in the same order, or <see langword="null"/> where unknown.</param>
    internal MultipleAssertionsFailedException(IReadOnlyList<string> failures, IReadOnlyList<string?> sites)
        : base(BuildMessage(failures, sites), null)
    {
        Failures = failures;
    }

    private static string BuildMessage(IReadOnlyList<string> failures, IReadOnlyList<string?> sites)
    {
        // The scope throws when it closes, far from the lines that made the assertions, so each one says where it was.
        var lines = string.Join(System.Environment.NewLine, failures.Select((f, i) =>
            sites[i] is { } site ? $"  [{i + 1}] {f}{System.Environment.NewLine}      at {site}" : $"  [{i + 1}] {f}"));
        return $"{failures.Count} assertion(s) failed:{System.Environment.NewLine}{lines}";
    }
}
