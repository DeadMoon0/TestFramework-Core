using System;
using System.Collections.Generic;

namespace TestFramework.Simple;

/// <summary>
/// Whether a message box is shown, decided from where the run is rather than from a switch someone has to set.
/// </summary>
/// <param name="Show">Whether to show the dialog.</param>
/// <param name="Reason">Why, for the log when it is not shown.</param>
internal sealed record MessageBoxDisplay(bool Show, string Reason)
{
    /// <summary>
    /// The variable that overrides the decision: <c>off</c> never shows the dialog, <c>on</c> always does.
    /// </summary>
    public const string SwitchVariable = "TESTFRAMEWORK_MESSAGEBOX";

    /// <summary>
    /// Variables a build agent sets, so a run there is known to have nobody at the screen.
    /// </summary>
    public static IReadOnlyList<string> CiMarkers { get; } = ["CI", "TF_BUILD"];

    /// <summary>
    /// Decides from the facts of the run's surroundings, which the caller reads.
    /// </summary>
    /// <param name="switchValue">The value of <see cref="SwitchVariable"/>, or <see langword="null"/>.</param>
    /// <param name="userInteractive">Whether the process runs in an interactive session.</param>
    /// <param name="onCi">Whether a build agent's marker is set.</param>
    /// <param name="onWindows">Whether the native dialog exists here at all.</param>
    /// <returns>The decision and its reason.</returns>
    public static MessageBoxDisplay Decide(string? switchValue, bool userInteractive, bool onCi, bool onWindows)
    {
        if (!onWindows)
            return new(false, "the dialog exists only on Windows");

        switch (switchValue?.Trim().ToLowerInvariant())
        {
            case "off":
                return new(false, $"{SwitchVariable} is off");
            case "on":
                return new(true, $"{SwitchVariable} is on");
        }

        if (onCi)
            return new(false, "running on a build agent, with nobody to answer it");

        return userInteractive
            ? new(true, "an interactive session")
            : new(false, "no interactive session to answer it");
    }
}
