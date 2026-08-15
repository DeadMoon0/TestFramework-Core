namespace TestFramework.Core.Debugger;

/// <summary>
/// Controls the built-in named-pipe debugger transport from code.
/// </summary>
/// <remarks>
/// By default the framework probes briefly for a debugger UI and, finding none, stays out of the
/// way for the rest of the process. Set <see cref="PipeDebuggerEnabled"/> to <see langword="true"/>
/// when a UI is expected — the connect timeout goes back up and a UI started mid-suite is still
/// picked up. Set it to <see langword="false"/> to skip the transport entirely, for example in CI.
/// The environment variable <c>TESTFRAMEWORK_DEBUG_PIPE</c> (<c>off</c>/<c>0</c>/<c>false</c> or
/// <c>on</c>/<c>1</c>/<c>true</c>) does the same thing without a code change; this property wins.
/// </remarks>
public static class TestFrameworkDebugging
{
    /// <summary>
    /// Gets or sets a value indicating whether the built-in pipe debugger may attach.
    /// </summary>
    /// <value>
    /// Reading returns <see langword="false"/> only when the transport is switched fully off.
    /// Writing pins the mode: <see langword="true"/> expects a UI, <see langword="false"/> disables
    /// the transport. Once written, the environment variable no longer applies.
    /// </value>
    public static bool PipeDebuggerEnabled
    {
        get => PipeTransport.GetMode() != PipeDebuggerMode.Off;
        set => PipeTransport.ModeOverride = value ? PipeDebuggerMode.On : PipeDebuggerMode.Off;
    }
}
