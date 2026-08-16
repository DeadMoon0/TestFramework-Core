using System;
using System.IO;
using TestFramework.Core.Debugger;

namespace TestFramework.Core.Tests;

/// <summary>
/// Holds run journalling off for the duration of a test.
/// </summary>
/// <remarks>
/// <para>
/// Journalling is armed by a folder existing, and that folder is created on any machine where the
/// tool has been installed. A test that asserts which debuggers are attached therefore passes or
/// fails according to whether the developer running it has the UI — which is not a property of the
/// code under test.
/// </para>
/// <para>
/// This points the journal at a path that does not exist, so a test that means "nothing is
/// journalling" says so instead of relying on the machine to be innocent.
/// </para>
/// </remarks>
internal sealed class JournalScope : IDisposable
{
    private const string DirectoryVariable = "TESTFRAMEWORK_DEBUG_JOURNAL_DIR";

    private readonly string? previous;

    private JournalScope(string? previous)
    {
        this.previous = previous;
    }

    /// <summary>Turns journalling off until the scope is disposed.</summary>
    internal static JournalScope Disarmed()
    {
        string? previous = System.Environment.GetEnvironmentVariable(DirectoryVariable);

        System.Environment.SetEnvironmentVariable(
            DirectoryVariable,
            Path.Combine(Path.GetTempPath(), "tf-no-journal-" + Guid.NewGuid().ToString("N")));

        DebugJournal.ResetForTests();

        return new JournalScope(previous);
    }

    public void Dispose()
    {
        System.Environment.SetEnvironmentVariable(DirectoryVariable, previous);
        DebugJournal.ResetForTests();
    }
}
