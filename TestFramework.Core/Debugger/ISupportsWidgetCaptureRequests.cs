using System;
using System.Threading.Tasks;

namespace TestFramework.Core.Debugger;

/// <summary>
/// Implemented by debuggers whose consumer can ask the run for fresh evidence.
/// </summary>
/// <remarks>
/// <para>
/// Internal, unlike <see cref="ISupportsWidgets"/>, and the difference is which way the message
/// travels. Reporting a widget is something any debugger might want to do, including one a caller
/// wrote; being asked for one requires a consumer on the other end of a live connection, which only
/// the framework's own transport has. A journal cannot be asked anything, and neither can a console.
/// </para>
/// <para>
/// The run installs its handler here rather than the debugger reaching for the run, because only the
/// run knows what it can photograph and only it can attribute the result.
/// </para>
/// </remarks>
internal interface ISupportsWidgetCaptureRequests
{
    /// <summary>
    /// Gets or sets what to do when the consumer asks for fresh evidence.
    /// </summary>
    /// <remarks>
    /// Null means nobody has offered to answer, and the debugger says exactly that rather than
    /// leaving the asker waiting.
    /// </remarks>
    Func<Task<WidgetCaptureOutcome>>? OnCaptureRequested { get; set; }
}

/// <summary>
/// What came of a capture request.
/// </summary>
/// <param name="Captured">How many widgets the run recorded while serving it.</param>
/// <param name="Detail">Why nothing was captured, when nothing was.</param>
internal readonly record struct WidgetCaptureOutcome(int Captured, string? Detail);
