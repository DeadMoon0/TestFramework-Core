using TestFrameworkCore.Logging.BuildInEvents;
using TestFrameworkCore.Timelines.Assertions;
using Xunit.Abstractions;

namespace TestFrameworkCore.Logging;

public class ScopedLogger
{
    int indentLevel = 0;
    LogLineWriter writer;
    bool _assertHeaderPrinted = false;
    AssertionScope? _assertionScope = null;

    internal void SetAssertionScope(AssertionScope scope) => _assertionScope = scope;
    internal void ClearAssertionScope() => _assertionScope = null;
    internal AssertionScope? CurrentScope => _assertionScope;

    internal ScopedLogger(ITestOutputHelper? outputHelper)
    {
        writer = new LogLineWriter(outputHelper, "\t");
    }

    public LogScopeDisposable EnterIndentScope()
    {
        this.indentLevel++;
        return new LogScopeDisposable(this);
    }

    public void ExitIndentScope()
    {
        this.indentLevel--;
    }

    internal void StartBuffering() => writer.StartBuffering();
    internal void StopBuffering() => writer.StopBuffering();
    internal void FlushBuffer() => writer.FlushBuffer();

    public void Log(LogEvent logEvent)
    {
        logEvent.CurrentIndentLevel = indentLevel;
        logEvent.FormatLogEvent(writer);
    }

    public void LogInformation(string log)
    {
        new InformationLogEvent(log, []) { CurrentIndentLevel = indentLevel }.FormatLogEvent(writer);
    }

    public void LogInformation(string format, params object[] args)
    {
        new InformationLogEvent(format, args) { CurrentIndentLevel = indentLevel }.FormatLogEvent(writer);
    }

    public void LogWarning(string log)
    {
        new WarningLogEvent(log, []) { CurrentIndentLevel = indentLevel }.FormatLogEvent(writer);
    }

    public void LogWarning(string format, params object[] args)
    {
        new WarningLogEvent(format, args) { CurrentIndentLevel = indentLevel }.FormatLogEvent(writer);
    }

    public void LogError(string log)
    {
        new ErrorLogEvent(log, []) { CurrentIndentLevel = indentLevel }.FormatLogEvent(writer);
    }

    public void LogError(string format, params object[] args)
    {
        new ErrorLogEvent(format, args) { CurrentIndentLevel = indentLevel }.FormatLogEvent(writer);
    }

    internal void EnsureAssertionHeaderPrinted()
    {
        if (_assertHeaderPrinted) return;
        _assertHeaderPrinted = true;
        LogInformation("");
        LogInformation("─────────────────────────────────────────────");
    }
}