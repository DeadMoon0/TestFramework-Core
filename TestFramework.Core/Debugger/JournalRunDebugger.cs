using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TestFramework.Core.Debugger;

/// <summary>
/// Records a run to disk as newline-delimited debug envelopes, so it stays inspectable after the
/// test host exits.
/// </summary>
/// <remarks>
/// This is what makes "open the UI after the run finished" work, and it is why the UI can list runs
/// it was never connected to. The lines are the same envelopes the pipe carries, so replaying a
/// journal drives exactly the code path a live run drives.
/// <para>
/// Writes ride the debugger signal queue that already exists, so no file I/O sits on a step's
/// execution path. The writer flushes each line rather than buffering: a killed test host is a
/// normal way for a run to end, and an unflushed tail would lose precisely the events explaining
/// why.
/// </para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class JournalRunDebugger : IRunDebugger, IDisposable, ISupportsWidgets, ISupportsLateAssertions
{
    private readonly object writeLock = new();
    private readonly bool enabled;

    private StreamWriter? writer;
    private DebugRunMetadata? metadata;
    private string? metadataPath;
    private string? journalPath;
    private long sequence;
    private long eventCount;
    private bool disposed;

    internal JournalRunDebugger()
    {
        enabled = DebugJournal.IsEnabled;
    }

    /// <summary>
    /// Gets a value indicating whether this debugger is recording the run.
    /// </summary>
    public bool IsCapturing => enabled && !disposed;

    /// <summary>
    /// Signals that a timeline run has been initialized, opening the journal for it.
    /// </summary>
    public Task SignalInitTimelineRunAsync(string sessionId, DebugRunStart start)
    {
        if (!IsCapturing)
            return Task.CompletedTask;

        BeginRun(sessionId, start.Identity.DisplayName, start.Identity.AssemblyPath, start.Identity);

        return AppendAsync(new PipeInitTimelineRunSignal
        {
            SessionId = sessionId,
            Name = start.Identity.DisplayName,
            ProjectPath = start.Identity.AssemblyPath,
            RunStructure = start.RunStructure,
            Identity = start.Identity
        });
    }

    /// <summary>
    /// Signals that a runtime entity changed lifecycle state.
    /// </summary>
    public Task SignalEntityTransitionAsync(string sessionId, DebugEntityTransition transition)
        => AppendAsync(new PipeEntityTransitionSignal
        {
            SessionId = sessionId,
            EntityKind = transition.EntityKind,
            Stage = transition.Stage,
            StepId = transition.StepId,
            State = transition.State,
            PreviousState = transition.PreviousState,
            OutcomeState = transition.OutcomeState,
            Failure = transition.Failure
        });

    /// <summary>
    /// Signals that a debugger-visible value has changed.
    /// </summary>
    public Task SignalValueUpdateAsync(string sessionId, DebugValueUpdate update)
        => AppendAsync(new PipeValueUpdateSignal
        {
            SessionId = sessionId,
            Name = update.Name,
            ValueKind = update.ValueKind,
            Stage = update.Stage,
            StepId = update.StepId,
            Envelope = update.Value
        });

    /// <summary>
    /// Signals that a structured log entry has been emitted for the active run.
    /// </summary>
    public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry)
        => AppendAsync(new PipeLogEntrySignal { SessionId = sessionId, Entry = entry });

    /// <summary>
    /// Signals a structured assertion result.
    /// </summary>
    public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry)
        => AppendAsync(new PipeAssertionSignal { SessionId = sessionId, Entry = entry });

    /// <summary>
    /// Records a piece of evidence a step or component produced.
    /// </summary>
    /// <remarks>
    /// The entry, not the file: the file is already in the run's own output, and the journal records
    /// where it is. That is what lets a recorded run be reopened weeks later with its screenshots
    /// still attached, and what keeps the journal from growing by a megabyte per picture.
    /// </remarks>
    public Task SignalWidgetAsync(string sessionId, DebugWidgetEntry entry)
        => AppendAsync(new PipeWidgetSignal { SessionId = sessionId, Entry = entry });

    /// <summary>
    /// Signals that the run finished, closing and finalizing the journal.
    /// </summary>
    public Task SignalTimelineRunFinishedAsync(string sessionId)
    {
        if (!IsCapturing)
            return Task.CompletedTask;

        AppendAsync(new PipeTimelineRunFinishedSignal { SessionId = sessionId }).GetAwaiter().GetResult();
        CompleteRun();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Appends an assertion made after the run finished to that run's journal, and counts it.
    /// </summary>
    /// <remarks>
    /// The journal is closed by then, but its file is where it was: reopened for the one line and closed again.
    /// A recorded run is what a selected run is rebuilt from, so a check missing here would be missing from the
    /// board the next time the run is opened, whatever the live connection delivered.
    /// </remarks>
    Task ISupportsLateAssertions.SignalLateAssertionAsync(string sessionId, DebugAssertionEntry entry)
    {
        if (!enabled)
            return Task.CompletedTask;

        try
        {
            lock (writeLock)
            {
                if (journalPath is null || metadata is null)
                    return Task.CompletedTask;

                DebugEnvelope envelope = DebugEnvelopeCodec.Wrap(
                    new PipeAssertionSignal { SessionId = sessionId, Entry = entry },
                    Interlocked.Increment(ref sequence));

                using (StreamWriter late = new(
                    new FileStream(journalPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
                {
                    late.WriteLine(DebugEnvelopeCodec.Serialize(envelope));
                }

                eventCount++;
                metadata = metadata with { EventCount = eventCount };
                WriteMetadata();
            }
        }
        catch (Exception e)
        {
            // As everywhere in the journal: an unwritable file never fails the test it only records.
            System.Diagnostics.Debug.WriteLine(e);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Does nothing. The journal never pauses a run, and never records that one was asked about.
    /// </summary>
    /// <remarks>
    /// Every step asks permission before it runs — the framework cannot know which steps a user
    /// marked, so it asks about all of them. That makes this a question rather than an event, and
    /// recording it would put one line per step into the journal describing something that did not
    /// happen. A run that really was held is held by the UI, which is the only party that knows.
    /// </remarks>
    public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId)
        => Task.CompletedTask;

    /// <summary>
    /// Closes the journal, marking the run finished if it had not already been closed.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        CompleteRun();
    }

    private void BeginRun(string sessionId, string name, string projectPath, TestIdentity? identity)
    {
        try
        {
            DebugJournal.Prune();

            string runs = DebugJournal.RunsDirectory;
            Directory.CreateDirectory(runs);

            DateTimeOffset startedAt = DateTimeOffset.UtcNow;
            string stem = DebugJournal.BuildFileStem(startedAt, sessionId);
            string journalFileName = stem + ".ndjson";

            lock (writeLock)
            {
                // FileShare.ReadWrite so a consumer can tail the journal while the run is still
                // writing it. Anything narrower makes a live run unreadable until it ends.
                journalPath = Path.Combine(runs, journalFileName);
                writer = new StreamWriter(
                    new FileStream(journalPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
                {
                    AutoFlush = true
                };

                metadataPath = Path.Combine(runs, stem + ".meta.json");
                metadata = new DebugRunMetadata
                {
                    ProtocolVersion = DebugProtocol.Version,
                    SessionId = sessionId,
                    Name = name,
                    ProjectPath = projectPath,
                    MachineName = System.Environment.MachineName,
                    StartedAtUtc = startedAt,
                    Outcome = DebugRunOutcome.Running,
                    JournalFileName = journalFileName,
                    Identity = identity
                };

                WriteMetadata();
            }
        }
        catch (Exception e)
        {
            // An unwritable journal must never fail the run it is only observing.
            System.Diagnostics.Debug.WriteLine(e);
            Close();
        }
    }

    private Task AppendAsync(IPipeSignal signal)
    {
        if (!IsCapturing)
            return Task.CompletedTask;

        try
        {
            lock (writeLock)
            {
                if (writer is null)
                    return Task.CompletedTask;

                DebugEnvelope envelope = DebugEnvelopeCodec.Wrap(signal, Interlocked.Increment(ref sequence));
                writer.WriteLine(DebugEnvelopeCodec.Serialize(envelope));
                eventCount++;
            }
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine(e);
            Close();
        }

        return Task.CompletedTask;
    }

    private void CompleteRun()
    {
        try
        {
            lock (writeLock)
            {
                if (metadata is not null)
                {
                    metadata = metadata with
                    {
                        FinishedAtUtc = DateTimeOffset.UtcNow,
                        Outcome = DebugRunOutcome.Finished,
                        EventCount = eventCount
                    };

                    WriteMetadata();
                }
            }
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine(e);
        }
        finally
        {
            Close();
        }
    }

    /// <summary>Caller must hold <see cref="writeLock"/>.</summary>
    private void WriteMetadata()
    {
        if (metadata is null || metadataPath is null)
            return;

        File.WriteAllText(metadataPath, JsonConvert.SerializeObject(metadata, Formatting.Indented, DebugJson.Settings));
    }

    private void Close()
    {
        lock (writeLock)
        {
            try
            {
                writer?.Dispose();
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e);
            }

            writer = null;
        }
    }
}
