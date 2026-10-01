using System;

namespace TestFramework.Core.Logging;

/// <summary>
/// Writes formatted log lines to the run's output, when it has one.
/// </summary>
public class LogLineWriter
{
    /// <summary>
    /// Gets the token used for a single indentation level.
    /// </summary>
    public string IndentLevelString { get; }

    private readonly Action<string>? writeLine;

    internal LogLineWriter(Action<string>? writeLine, string indentLevelString)
    {
        this.writeLine = writeLine;
        IndentLevelString = indentLevelString;
    }

    /// <summary>
    /// Writes a formatted line to the output stream or to the internal buffer when buffering is active.
    /// </summary>
    /// <param name="format">The format string or plain line text.</param>
    /// <param name="args">Optional format arguments.</param>
    public void WriteLine(string format, params object[] args)
    {
        if (writeLine is null) return;
        string line = args.Length == 0 ? format : string.Format(format, args);
        writeLine(line);
    }
}