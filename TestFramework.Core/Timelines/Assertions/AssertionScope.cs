using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TestFramework.Core.Logging;
using TestFramework.Core.Exceptions;

namespace TestFramework.Core.Timelines.Assertions;

/// <summary>
/// Collects assertion failures and throws them together when the scope is disposed.
/// </summary>
public class AssertionScope : IDisposable
{
    private readonly ScopedLogger? _logger;
    private readonly List<string> _failures = [];
    private bool _disposed = false;

    internal AssertionScope(ScopedLogger? logger)
    {
        _logger = logger;
        _logger?.SetAssertionScope(this);
    }

    internal void RecordFailure(string message)
    {
        _failures.Add(message);
    }

    /// <summary>
    /// Ends the assertion scope and throws when one or more failures were recorded.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _logger?.ClearAssertionScope();

        if (_failures.Count == 0) return;

        throw new MultipleAssertionsFailedException(new ReadOnlyCollection<string>(_failures));
    }
}
