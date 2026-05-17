namespace TestFramework.Core.Debugger;

/// <summary>
/// Describes the severity of a structured debugger log entry.
/// </summary>
public enum DebugLogLevel
{
    /// <summary>
    /// Informational execution output.
    /// </summary>
    Information,

    /// <summary>
    /// A warning that does not stop execution.
    /// </summary>
    Warning,

    /// <summary>
    /// An error-level execution message.
    /// </summary>
    Error
}