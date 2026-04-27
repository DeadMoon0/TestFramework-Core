using System.Collections.Generic;
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

    private ITestOutputHelper? outputHelper;
    private bool _isBuffering = false;
    private readonly List<string> _buffer = [];

    internal LogLineWriter(ITestOutputHelper? outputHelper, string indentLevelString)
    {
        this.outputHelper = outputHelper;
        IndentLevelString = indentLevelString;
    }

    internal void StartBuffering() => _isBuffering = true;
    internal void StopBuffering() => _isBuffering = false;
    internal void FlushBuffer()
    {
        foreach (var line in _buffer)
            outputHelper?.WriteLine(line);
        _buffer.Clear();
    }

    /// <summary>
    /// Writes a formatted line to the output stream or to the internal buffer when buffering is active.
    /// </summary>
    /// <param name="format">The format string or plain line text.</param>
    /// <param name="args">Optional format arguments.</param>
    public void WriteLine(string format, params object[] args)
    {
        if (outputHelper is null) return;
        if (_isBuffering)
        {
            _buffer.Add(args.Length == 0 ? format : string.Format(format, args));
            return;
        }
        if (args.Length == 0) outputHelper.WriteLine(format);
        else outputHelper.WriteLine(format, args);
    }
}