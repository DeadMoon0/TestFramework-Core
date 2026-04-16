using System;

namespace TestFrameworkCore.Logging;

public class LogScopeDisposable : IDisposable
{
    ScopedLogger scopedLogger;

    internal LogScopeDisposable(ScopedLogger scopedLogger)
    {
        this.scopedLogger = scopedLogger;
    }

    public void Dispose()
    {
        scopedLogger.ExitIndentScope();
    }
}
