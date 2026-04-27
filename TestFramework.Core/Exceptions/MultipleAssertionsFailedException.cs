using System;
using System.Collections.Generic;
using System.Linq;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when an <see cref="Timelines.Assertions.AssertionScope"/> collects multiple assertion failures.
/// </summary>
public class MultipleAssertionsFailedException : Exception
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
        : base(BuildMessage(failures))
    {
        Failures = failures;
    }

    private static string BuildMessage(IReadOnlyList<string> failures)
    {
        var lines = string.Join(System.Environment.NewLine, failures.Select((f, i) => $"  [{i + 1}] {f}"));
        return $"{failures.Count} assertion(s) failed:{System.Environment.NewLine}{lines}";
    }
}
