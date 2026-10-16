using System;

namespace TestFramework.Core.Debugger;

internal static class PipeTransport
{
    private const string DefaultPipeName = "TestFrameworkDebug_79d7aa2d-da07-4c84-b1f2-0639b0009290";

    /// <summary>Full connect timeout, used only when a UI is known to be expected.</summary>
    private static readonly TimeSpan AttachedConnectTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Probe timeout for <see cref="PipeDebuggerMode.Auto"/>. Long enough to win the normal
    /// connect race against a UI that is already listening, short enough that a suite with no UI
    /// does not pay seconds per run.
    /// </summary>
    private static readonly TimeSpan ProbeConnectTimeout = TimeSpan.FromMilliseconds(250);

    private static readonly TimeSpan DefaultWaitTimeout = TimeSpan.FromSeconds(600);

    /// <summary>Bounds the write path so a UI that stops draining cannot stall every signal.</summary>
    internal static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Set through <see cref="TestFrameworkDebugging.PipeDebuggerEnabled"/>. Takes precedence over
    /// the environment variable so a test project can opt in or out in code.
    /// </summary>
    internal static PipeDebuggerMode? ModeOverride { get; set; }

    internal static string GetPipeName()
    {
        // TESTFRAMEWORK_DEBUG_PIPE_NAME is an isolation override, never an enable switch: the DebugUI's
        // zero-config flow leaves it unset, so the mode must not be derived from it.
        return System.Environment.GetEnvironmentVariable("TESTFRAMEWORK_DEBUG_PIPE_NAME") ?? DefaultPipeName;
    }

    internal static PipeDebuggerMode GetMode()
    {
        return ModeOverride ?? ParseMode(System.Environment.GetEnvironmentVariable("TESTFRAMEWORK_DEBUG_PIPE"));
    }

    private static PipeDebuggerMode ParseMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return PipeDebuggerMode.Auto;

        string trimmed = value.Trim();

        if (string.Equals(trimmed, "off", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "0", StringComparison.Ordinal)
            || string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase))
            return PipeDebuggerMode.Off;

        if (string.Equals(trimmed, "on", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "1", StringComparison.Ordinal)
            || string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase))
            return PipeDebuggerMode.On;

        return PipeDebuggerMode.Auto;
    }

    internal static TimeSpan GetConnectTimeout()
    {
        if (GetMode() == PipeDebuggerMode.On)
            return AttachedConnectTimeout;

        string? configured = System.Environment.GetEnvironmentVariable("TESTFRAMEWORK_DEBUG_PIPE_PROBE_MS");
        return int.TryParse(configured, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int milliseconds) && milliseconds > 0
            ? TimeSpan.FromMilliseconds(milliseconds)
            : ProbeConnectTimeout;
    }

    internal static TimeSpan GetWaitTimeout()
    {
        string? configured = System.Environment.GetEnvironmentVariable("TESTFRAMEWORK_DEBUG_PIPE_WAIT_MS");
        return int.TryParse(configured, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int milliseconds) && milliseconds > 0
            ? TimeSpan.FromMilliseconds(milliseconds)
            : DefaultWaitTimeout;
    }
}
