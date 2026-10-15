using System;
using System.Diagnostics;
using System.IO;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;

namespace TestFramework.Core.Timelines.Assertions;

/// <summary>
/// How every asserter reports a failure: collected by an open <see cref="AssertionScope"/>, thrown otherwise, and
/// in both cases with the line that made the assertion.
/// </summary>
internal static class AssertionFailure
{
    /// <summary>
    /// Records the failure in the open scope, or throws it.
    /// </summary>
    /// <param name="logger">The run's logger, which knows the open scope.</param>
    /// <param name="message">What was asserted and what was found.</param>
    /// <param name="create">Builds the exception to throw when no scope is open.</param>
    public static void Raise(ScopedLogger? logger, string message, Func<string, string?, AssertionFailedException> create)
    {
        string? site = Site();
        if (logger?.CurrentScope is { } scope)
        {
            scope.RecordFailure(message, site);
            return;
        }

        throw create(message, site);
    }

    /// <summary>
    /// The first caller outside Core: the test line that wrote the assertion.
    /// </summary>
    /// <remarks>
    /// Read from the stack because an assertion's entry points are fluent members a test chains, and
    /// caller-information parameters would put optional parameters on every one of them. Outside Core and outside
    /// the runtime, because an assertion made inside a run has no test line above it - and naming a thread-pool
    /// frame would be worse than naming nothing.
    /// </remarks>
    private static string? Site()
    {
        StackFrame[] frames = new StackTrace(2, fNeedFileInfo: true).GetFrames();
        foreach (StackFrame frame in frames)
        {
            Type? type = frame.GetMethod()?.DeclaringType;
            if (type is null || type.Assembly == typeof(AssertionFailure).Assembly)
                continue;

            if (type.Assembly.GetName().Name is { } name && (name.StartsWith("System.", StringComparison.Ordinal) || name == "System"))
                return null;

            return frame.GetFileName() is { Length: > 0 } file
                ? $"{Path.GetFileName(file)}:{frame.GetFileLineNumber()}"
                : null;
        }

        return null;
    }
}
