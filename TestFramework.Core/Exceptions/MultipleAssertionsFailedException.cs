using System;
using System.Collections.Generic;
using System.Linq;

namespace TestFramework.Core.Exceptions;

public class MultipleAssertionsFailedException : Exception
{
    public IReadOnlyList<string> Failures { get; }

    public MultipleAssertionsFailedException(IReadOnlyList<string> failures)
        : base(BuildMessage(failures))
    {
        Failures = failures;
    }

    private static string BuildMessage(IReadOnlyList<string> failures)
    {
        var lines = string.Join(Environment.NewLine, failures.Select((f, i) => $"  [{i + 1}] {f}"));
        return $"{failures.Count} assertion(s) failed:{Environment.NewLine}{lines}";
    }
}
