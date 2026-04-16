using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TestFramework.Core.Logging;
using TestFrameworkCore.Exceptions;

namespace TestFramework.Core.Timelines.Assertions;

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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _logger?.ClearAssertionScope();

        if (_failures.Count == 0) return;

        throw new MultipleAssertionsFailedException(new ReadOnlyCollection<string>(_failures));
    }
}
