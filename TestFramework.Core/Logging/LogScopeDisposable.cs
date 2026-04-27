using System;

namespace TestFramework.Core.Logging;

/// <summary>
/// Represents a disposable indentation scope for a <see cref="ScopedLogger"/>.
/// </summary>
public class LogScopeDisposable : IDisposable
{
    ScopedLogger scopedLogger;

    internal LogScopeDisposable(ScopedLogger scopedLogger)
    {
        this.scopedLogger = scopedLogger;
    }

    /// <summary>
    /// Ends the indentation scope on the owning logger.
    /// </summary>
    public void Dispose()
    {
        scopedLogger.ExitIndentScope();
    }
}
