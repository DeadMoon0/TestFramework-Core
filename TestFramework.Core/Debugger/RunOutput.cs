using System;
using System.IO;
using System.Linq;

namespace TestFramework.Core.Debugger;

/// <summary>
/// Where a run writes the files it produces for whoever reads it afterwards.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not the debug journal. The journal is a recording the DebugUI keeps for itself, gated
/// on the UI being installed and pruned behind the user's back — none of which is right for something
/// a build is meant to publish. This is the run's own output: it exists whether or not any tool is
/// installed, nothing deletes it, and a pipeline publishes the folder as artifacts without knowing
/// anything about the framework.
/// </para>
/// <para>
/// That is the whole integration story. A build sets one environment variable to its staging
/// directory and adds a publish task; everything a run writes shows up beside the test results.
/// </para>
/// </remarks>
public static class RunOutput
{
    /// <summary>The environment variable a build sets to redirect run output into its staging area.</summary>
    public const string DirectoryVariable = "TESTFRAMEWORK_OUTPUT";

    private const string DefaultFolderName = "TestFrameworkOutput";

    /// <summary>
    /// Gets the directory under which every run's output folder is created.
    /// </summary>
    /// <remarks>
    /// Read each time rather than resolved once, because a test that sets the variable to a temporary
    /// directory has to be able to take effect, and reading an environment variable is not a cost
    /// worth caching against.
    /// </remarks>
    public static string Root
        // Fully qualified: TestFramework.Core.Environment is a namespace in this assembly, so the
        // unqualified name binds to it rather than to System.Environment.
        => System.Environment.GetEnvironmentVariable(DirectoryVariable) is { Length: > 0 } configured
            ? configured
            : Path.Combine(Directory.GetCurrentDirectory(), DefaultFolderName);

    /// <summary>
    /// Builds the folder name for one run: readable first, unique second.
    /// </summary>
    /// <remarks>
    /// The identifier is appended rather than used alone because these folders are browsed by people
    /// looking for the run that failed. A wall of GUIDs makes them open folders one at a time; a name
    /// with a short suffix stays sortable, stays unique across parallel runs on one agent, and can be
    /// scanned.
    /// </remarks>
    internal static string FolderNameFor(string? testName, string sessionId)
    {
        string suffix = sessionId.Length >= 8 ? sessionId[..8] : sessionId;

        return string.IsNullOrWhiteSpace(testName)
            ? suffix
            : $"{SafeName(testName!, 80)}-{suffix}";
    }

    /// <summary>
    /// Turns arbitrary text into something a file system will accept as one path segment.
    /// </summary>
    /// <remarks>
    /// Test names and variable identifiers are written by people, so they contain spaces, colons,
    /// slashes and angle brackets from generic parameters. None of that can reach a path unfiltered.
    /// </remarks>
    internal static string SafeName(string name, int maxLength)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string cleaned = new([.. name.Select(c => invalid.Contains(c) || c is ' ' ? '_' : c)]);

        cleaned = cleaned.Trim('_', '.');

        if (cleaned.Length == 0)
            cleaned = "value";

        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }
}
