namespace TestFramework.Core.Debugger;

/// <summary>
/// Implemented by a debugger that is a display rather than a transport.
/// </summary>
/// <remarks>
/// <para>
/// Kept off <see cref="IRunDebugger"/> deliberately, in the same way as
/// <see cref="ISupportsRunCancellation"/>: a debugger that serialises has no use for a rendered line, and
/// making every one of them accept the console's output is how the console's output ended up in the journal
/// and on the pipe in the first place.
/// </para>
/// <para>
/// The console is the one consumer that genuinely wants the framework's own narration — the rules, the padded
/// tables, the indentation. It receives it here, in process, and it is the only thing that does.
/// </para>
/// </remarks>
internal interface ISupportsRenderedLog
{
    /// <summary>Takes the lines an event rendered to, and where in the run they belong.</summary>
    void WriteRenderedLog(string[] lines, LogPlacement placement);
}

/// <summary>Where a rendered line goes: the run, a stage, or one attempt at a step.</summary>
/// <remarks>
/// The indentation depth is here because it is a console measurement. It used to travel with every entry on
/// every transport, describing a layout only one consumer has.
/// </remarks>
internal readonly record struct LogPlacement(string? Stage, int? StepId, int? Iteration, int IndentLevel);
