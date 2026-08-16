using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace TestFramework.Core.Debugger;

/// <summary>
/// Resolves where run journals live and whether writing them is switched on at all.
/// </summary>
/// <remarks>
/// The gate is the existence of the journal root, which the DebugUI creates when it is installed.
/// That keeps the promise that a missing UI costs a run nothing: a machine that has never had the
/// UI performs one directory check per process and never touches the disk again. It also means
/// there is no configuration step — installing the tool is what switches on the durability the tool
/// needs.
/// </remarks>
internal static class DebugJournal
{
    private const string RootFolderName = "TestFramework";
    private const string JournalFolderName = "Debug";
    private const string RunsFolderName = "runs";
    private const int DefaultRetainedRuns = 50;

    private static readonly object StateLock = new();
    private static bool resolved;
    private static string? resolvedRoot;
    private static bool resolvedEnabled;

    /// <summary>The journal root, whether or not it exists.</summary>
    internal static string Root
    {
        get
        {
            EnsureResolved();
            return resolvedRoot!;
        }
    }

    /// <summary>Whether run journals should be written by this process.</summary>
    internal static bool IsEnabled
    {
        get
        {
            EnsureResolved();
            return resolvedEnabled;
        }
    }

    internal static string RunsDirectory => Path.Combine(Root, RunsFolderName);

    /// <summary>Re-reads the environment and the marker. Tests need this; nothing else should.</summary>
    internal static void ResetForTests()
    {
        lock (StateLock)
        {
            resolved = false;
            resolvedRoot = null;
            resolvedEnabled = false;
        }
    }

    private static void EnsureResolved()
    {
        lock (StateLock)
        {
            if (resolved)
                return;

            resolvedRoot = ResolveRoot();

            // One directory check per process. Deliberately not created here: creating it would arm
            // journalling on every machine that ever ran a test, which is exactly what the marker
            // exists to avoid.
            resolvedEnabled = Directory.Exists(resolvedRoot);
            resolved = true;
        }
    }

    private static string ResolveRoot()
    {
        string? configured = System.Environment.GetEnvironmentVariable("TESTFRAMEWORK_DEBUG_JOURNAL_DIR");
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        return Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            RootFolderName,
            JournalFolderName);
    }

    internal static int GetRetainedRunCount()
    {
        string? configured = System.Environment.GetEnvironmentVariable("TESTFRAMEWORK_DEBUG_JOURNAL_KEEP");
        return int.TryParse(configured, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) && count > 0
            ? count
            : DefaultRetainedRuns;
    }

    /// <summary>
    /// Builds a file-system-safe stem for a run: sortable timestamp first so the newest runs are
    /// obvious both to the retention sweep and to a human looking at the folder.
    /// </summary>
    internal static string BuildFileStem(DateTimeOffset startedAtUtc, string sessionId)
    {
        string timestamp = startedAtUtc.UtcDateTime.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture);
        return $"{timestamp}-{Sanitize(sessionId)}";
    }

    private static string Sanitize(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        char[] sanitized = value.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray();
        string result = new(sanitized);
        return result.Length == 0 ? "run" : result;
    }

    /// <summary>
    /// Opens a journal or metadata file for reading, tolerating a run that is still writing it.
    /// </summary>
    /// <remarks>
    /// The plain <c>File.ReadAllText</c>/<c>ReadAllLines</c> helpers request <see cref="FileShare.Read"/>,
    /// which conflicts with the writer's open write handle — so reading a live run fails with a
    /// sharing violation. A consumer must allow writers explicitly, which is easy to get wrong and
    /// only shows up against an in-progress run.
    /// </remarks>
    internal static StreamReader OpenForReading(string path)
        => new(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

    /// <summary>
    /// Deletes the oldest runs beyond the retention limit. Called once per run rather than on a
    /// timer, so an idle machine does no work.
    /// </summary>
    internal static void Prune()
    {
        try
        {
            string runs = RunsDirectory;
            if (!Directory.Exists(runs))
                return;

            int keep = GetRetainedRunCount();

            List<string> stems = Directory
                .EnumerateFiles(runs, "*.meta.json")
                .Select(path => Path.GetFileName(path)[..^".meta.json".Length])
                .OrderByDescending(stem => stem, StringComparer.Ordinal)
                .ToList();

            foreach (string stem in stems.Skip(keep))
            {
                TryDelete(Path.Combine(runs, stem + ".meta.json"));
                TryDelete(Path.Combine(runs, stem + ".ndjson"));
            }
        }
        catch (Exception e)
        {
            // Retention is housekeeping. A run must never fail because old files could not be tidied.
            System.Diagnostics.Debug.WriteLine(e);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine(e);
        }
    }
}

/// <summary>
/// How a run ended, as recorded in its metadata sidecar.
/// </summary>
internal enum DebugRunOutcome
{
    /// <summary>Written at start. A journal still saying this was never closed — the host died.</summary>
    Running,

    /// <summary>The run reached its finish signal.</summary>
    Finished
}

/// <summary>
/// The per-run metadata sidecar. Kept separate from the event log so listing runs never requires
/// parsing them.
/// </summary>
internal sealed record DebugRunMetadata
{
    public required int ProtocolVersion { get; init; }
    public required string SessionId { get; init; }
    public required string Name { get; init; }
    public required string ProjectPath { get; init; }
    public required string MachineName { get; init; }
    public required DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset? FinishedAtUtc { get; init; }
    public required DebugRunOutcome Outcome { get; init; }
    public required string JournalFileName { get; init; }
    public long EventCount { get; init; }

    /// <summary>
    /// The test that produced the run. Kept in the sidecar so the run picker can offer re-run for a
    /// completed run without opening its journal.
    /// </summary>
    public TestIdentity? Identity { get; init; }
}
