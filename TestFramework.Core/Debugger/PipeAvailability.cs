using System;
using System.Diagnostics;
using System.IO;

namespace TestFramework.Core.Debugger;

/// <summary>
/// Answers "is a debugger UI listening right now?" cheaply enough to ask on every run.
/// </summary>
/// <remarks>
/// A connect attempt is the expensive way to find out — it costs its full timeout when nothing is
/// there, which is why the transport used to latch the miss for the whole process and never notice
/// a UI that started later. On Windows a named pipe is visible in the object namespace, so testing
/// for the path answers the same question for the price of a file check and no latch is needed.
/// The result is cached briefly because <c>IsCapturing</c> is consulted on every variable write.
/// </remarks>
internal static class PipeAvailability
{
    /// <summary>
    /// Long enough that a burst of value updates costs one probe, short enough that attaching a UI
    /// mid-run is picked up immediately in human terms.
    /// </summary>
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMilliseconds(500);

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, CacheEntry> Cache = new(StringComparer.Ordinal);

    private readonly record struct CacheEntry(long Timestamp, bool Listening);

    internal static bool IsListening(string pipeName)
    {
        if (Cache.TryGetValue(pipeName, out CacheEntry entry)
            && Stopwatch.GetElapsedTime(entry.Timestamp) < CacheDuration)
            return entry.Listening;

        bool listening = Probe(pipeName);
        Cache[pipeName] = new CacheEntry(Stopwatch.GetTimestamp(), listening);
        return listening;
    }

    /// <summary>Drops the cached answers so a test can observe a pipe appearing or disappearing.</summary>
    internal static void ResetForTests() => Cache.Clear();

    /// <summary>
    /// The prefix the runtime gives the socket file backing a named pipe on Unix.
    /// </summary>
    /// <remarks>
    /// An implementation detail of System.IO.Pipes rather than a documented contract, which is why
    /// <c>ProbeReportsAListenerWhileAServerIsOpen</c> runs on every platform: if the runtime ever
    /// renamed this, that test fails rather than the feature quietly ceasing to find a UI.
    /// </remarks>
    private const string UnixPipePrefix = "CoreFxPipe_";

    private static bool Probe(string pipeName)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                // A named pipe on Unix is a socket file under the temp directory, so the name is a
                // path here too. Stating it with File.Exists only stats it -- measured on Linux, a
                // pending WaitForConnectionAsync is still pending afterwards -- so the Windows
                // hazard described below does not apply, and unlike answering "possibly listening"
                // this costs no connect attempt on a machine with no UI at all.
                return File.Exists(Path.Combine(Path.GetTempPath(), UnixPipePrefix + pipeName));
            }

            // Enumerate the pipe directory rather than testing the pipe path directly.
            // File.Exists(@"\\.\pipe\name") looks like the obvious check and is actively harmful:
            // opening a pipe path *connects* to it, which completes the UI's pending
            // WaitForConnectionAsync with a phantom client that immediately vanishes. The UI would
            // see a connect/disconnect for every probe, and while it held a single server instance
            // the real run could not get in at all. Directory enumeration only lists names.
            foreach (string path in Directory.EnumerateFiles(@"\\.\pipe\"))
            {
                if (string.Equals(Path.GetFileName(path), pipeName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
        catch (Exception e)
        {
            // An inaccessible object namespace is not proof of absence; fall back to connecting.
            Debug.WriteLine(e);
            return true;
        }
    }
}
