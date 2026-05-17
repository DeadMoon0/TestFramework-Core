using System;
using Xunit.Abstractions;

namespace TestFramework.Core.Logging;

/// <summary>
/// Writes formatted log lines to the configured xUnit output helper, with optional buffering support.
/// </summary>
public class LogLineWriter
{
    /// <summary>
    /// Gets the token used for a single indentation level.
    /// </summary>
    public string IndentLevelString { get; }

    private readonly ITestOutputHelper? outputHelper;

    internal LogLineWriter(ITestOutputHelper? outputHelper, string indentLevelString)
    {
        this.outputHelper = outputHelper;
        IndentLevelString = indentLevelString;
    }

    /// <summary>
    /// Writes a formatted line to the output stream or to the internal buffer when buffering is active.
    /// </summary>
    /// <param name="format">The format string or plain line text.</param>
    /// <param name="args">Optional format arguments.</param>
    public void WriteLine(string format, params object[] args)
    {
        if (outputHelper is null) return;
        string line = args.Length == 0 ? format : string.Format(format, args);
        outputHelper.WriteLine(line);
    }
}