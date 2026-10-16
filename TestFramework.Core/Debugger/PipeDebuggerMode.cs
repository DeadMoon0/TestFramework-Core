namespace TestFramework.Core.Debugger;

/// <summary>
/// Describes how eagerly the built-in named-pipe debugger transport tries to reach a debugger UI.
/// </summary>
internal enum PipeDebuggerMode
{
    /// <summary>Never attach the pipe debugger.</summary>
    Off,

    /// <summary>Probe briefly for a UI; give up cheaply and stay out of the way when there is none.</summary>
    Auto,

    /// <summary>A UI is expected: wait the full connect timeout and keep retrying between runs.</summary>
    On
}
