using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TestFramework.Core.Exceptions;

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

/// <summary>
/// Rebuilds protocol messages from their serialized form.
/// </summary>
public static class PipeSignalFactory
{
    /// <summary>
    /// Rebuilds a signal from an envelope payload.
    /// </summary>
    /// <remarks>
    /// The discriminator stays an explicit switch on <see cref="PipeSignalKind"/> rather than
    /// reflection-driven polymorphism: this deserializes data arriving over a local socket, and
    /// letting the payload name its own CLR type would be a deserialization hole.
    /// </remarks>
    public static IPipeSignal DeserializePayload(PipeSignalKind kind, Newtonsoft.Json.Linq.JObject payload)
        => DeserializeSignal(kind, payload.ToString(Formatting.None));

    /// <summary>
    /// Rebuilds a signal from its serialized form, reading the discriminator out of the JSON.
    /// </summary>
    public static IPipeSignal DeserializeSignal(string json)
    {
        PipeSignalKind signalKind = (JsonConvert.DeserializeAnonymousType(json, new { Kind = (PipeSignalKind)0 }, DebugJson.Settings)
            ?? throw new FrameworkStateException("Could not deserialize pipe signal."))
            .Kind;

        return DeserializeSignal(signalKind, json);
    }

    private static IPipeSignal DeserializeSignal(PipeSignalKind signalKind, string json)
    {
        return signalKind switch
        {
            PipeSignalKind.InitTimelineRun => JsonConvert.DeserializeObject<PipeInitTimelineRunSignal>(json, DebugJson.Settings) ?? throw new FrameworkStateException("Could not deserialize init signal."),
            PipeSignalKind.EntityTransition => JsonConvert.DeserializeObject<PipeEntityTransitionSignal>(json, DebugJson.Settings) ?? throw new FrameworkStateException("Could not deserialize entity transition signal."),
            PipeSignalKind.ValueUpdate => JsonConvert.DeserializeObject<PipeValueUpdateSignal>(json, DebugJson.Settings) ?? throw new FrameworkStateException("Could not deserialize value update signal."),
            PipeSignalKind.LogEntry => JsonConvert.DeserializeObject<PipeLogEntrySignal>(json, DebugJson.Settings) ?? throw new FrameworkStateException("Could not deserialize log entry signal."),
            PipeSignalKind.Assertion => JsonConvert.DeserializeObject<PipeAssertionSignal>(json, DebugJson.Settings) ?? throw new FrameworkStateException("Could not deserialize assertion signal."),
            PipeSignalKind.BreakpointHitRequest => JsonConvert.DeserializeObject<PipeBreakpointHitRequestSignal>(json, DebugJson.Settings) ?? throw new FrameworkStateException("Could not deserialize breakpoint request signal."),
            PipeSignalKind.BreakpointHitContinue => JsonConvert.DeserializeObject<PipeBreakpointHitContinueSignal>(json, DebugJson.Settings) ?? throw new FrameworkStateException("Could not deserialize breakpoint continue signal."),
            PipeSignalKind.TimelineRunFinished => JsonConvert.DeserializeObject<PipeTimelineRunFinishedSignal>(json, DebugJson.Settings) ?? throw new FrameworkStateException("Could not deserialize timeline-finished signal."),
            PipeSignalKind.CancelRun => JsonConvert.DeserializeObject<PipeCancelRunSignal>(json, DebugJson.Settings) ?? throw new FrameworkStateException("Could not deserialize cancel-run signal."),
            _ => throw new ArgumentOutOfRangeException(nameof(signalKind), signalKind, "Unsupported pipe signal kind.")
        };
    }
}

internal sealed class PipeProtocolStream(PipeStream stream)
{
    private readonly SemaphoreSlim sendLock = new(1, 1);

    /// <summary>
    /// One connection carries one run, so a per-connection counter is also per-session. Sent with
    /// every frame so a consumer can spot gaps and drop duplicates when replaying.
    /// </summary>
    private long sequence;

    internal bool PipeIsDead { get; private set; }

    internal async Task SendSignalAsync(IPipeSignal signal)
    {
        if (PipeIsDead)
            return;

        bool lockAcquired = false;
        try
        {
            await sendLock.WaitAsync();
            lockAcquired = true;

            byte[] buffer = DebugEnvelopeCodec.EncodeFrame(
                DebugEnvelopeCodec.Wrap(signal, Interlocked.Increment(ref sequence)));

            // A UI that stops reading applies backpressure through the pipe buffer. Without a bound
            // here every subsequent signal would queue behind this write for the rest of the run.
            using CancellationTokenSource writeCancellation = new(PipeTransport.WriteTimeout);
            await stream.WriteAsync(buffer, 0, buffer.Length, writeCancellation.Token);
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            PipeIsDead = true;
        }
        finally
        {
            if (lockAcquired)
                sendLock.Release();
        }
    }

    internal async Task<IPipeSignal?> WaitSignalAsync(CancellationToken cancellationToken = default)
    {
        DebugEnvelope? envelope = await WaitEnvelopeAsync(cancellationToken);
        if (envelope is null)
            return null;

        try
        {
            return DebugEnvelopeCodec.Unwrap(envelope);
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            PipeIsDead = true;
            return null;
        }
    }

    internal async Task<DebugEnvelope?> WaitEnvelopeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            byte[] lenBuf = new byte[sizeof(int)];
            await stream.ReadExactlyAsync(lenBuf, 0, lenBuf.Length, cancellationToken);
            int messageLength = BitConverter.ToInt32(lenBuf);
            if (messageLength <= 0 || messageLength > DebugEnvelopeCodec.MaxMessageBytes)
                throw new InvalidDataException($"Invalid pipe frame length: {messageLength}");

            byte[] jsonBuf = new byte[messageLength];
            await stream.ReadExactlyAsync(jsonBuf, 0, jsonBuf.Length, cancellationToken);
            return DebugEnvelopeCodec.Deserialize(DebugEnvelopeCodec.WireEncoding.GetString(jsonBuf));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            PipeIsDead = true;
            return null;
        }
    }
}

internal sealed class PipeClient : IDisposable
{
    /// <summary>
    /// Reports whether a UI is listening on the named pipe, cheaply enough to ask per run, so
    /// callers can skip setting up a debugger that has nowhere to send its signals.
    /// </summary>
    internal static bool IsKnownUnavailable(string pipeName)
        => PipeTransport.GetMode() != PipeDebuggerMode.On && !PipeAvailability.IsListening(pipeName);

    /// <summary>Drops cached availability so a test can observe a pipe appearing or disappearing.</summary>
    internal static void ResetAvailabilityForTests() => PipeAvailability.ResetForTests();

    private readonly string pipeName;
    private readonly SemaphoreSlim connectionLock = new(1, 1);
    private readonly object stateLock = new();
    private NamedPipeClientStream? pipeClient;
    private PipeProtocolStream? stream;
    private bool disposed;

    /// <summary>
    /// One reader owns the stream. Targeted reads register here instead of reading directly, which
    /// is what lets unsolicited messages — cancellation — arrive at any time rather than only while
    /// something happens to be waiting.
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<PipeSignalKind, TaskCompletionSource<IPipeSignal>> waiters = new();

    private CancellationTokenSource? receiveCancellation;
    private Task? receiveLoop;

    /// <summary>
    /// Raised when the consumer asks this run to stop. Delivered on the receive loop.
    /// </summary>
    internal event Action<string?>? CancellationRequested;

    internal PipeClient(string pipeName)
    {
        this.pipeName = pipeName;
    }

    /// <summary>
    /// Reports whether signals sent through this client can still reach a UI: either one is already
    /// connected, or the transport is enabled and this process has not yet found the pipe empty.
    /// </summary>
    internal bool IsAttachedOrCouldAttach
    {
        get
        {
            if (disposed)
                return false;

            if (IsConnected)
                return true;

            return PipeTransport.GetMode() != PipeDebuggerMode.Off && !IsKnownUnavailable(pipeName);
        }
    }

    private bool IsConnected
    {
        get
        {
            lock (stateLock)
            {
                return pipeClient?.IsConnected == true && stream is not null && !stream.PipeIsDead;
            }
        }
    }

    internal async Task SignalAsync(IPipeSignal signal)
    {
        if (!await EnsureConnectedAsync())
            return;

        PipeProtocolStream? currentStream = GetConnectedStream();
        if (currentStream is null)
            return;

        await currentStream.SendSignalAsync(signal);
        if (currentStream.PipeIsDead)
            DisposeConnection(currentStream);
    }

    /// <summary>
    /// Sends a signal and waits for the reply it expects, or gives up after <paramref name="timeout"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The waiter is registered <em>before</em> the request goes out, and that ordering is the whole
    /// point of this method existing. Reads happen on their own loop, so a consumer answering over a
    /// local pipe can reply before the sending continuation resumes; an answer that arrives with no
    /// waiter registered is dropped, and the caller then waits out the full timeout for a reply that
    /// already came and went. Sending first made every step a race, and losing it cost ten minutes.
    /// </para>
    /// <para>
    /// On expiry the connection is left intact. The receive loop owns framing, so an unanswered wait
    /// no longer implies a half-read frame the way a direct read did.
    /// </para>
    /// </remarks>
    internal async Task<IPipeSignal?> ExchangeAsync(IPipeSignal request, PipeSignalKind reply, TimeSpan? timeout = null)
    {
        if (!await EnsureConnectedAsync())
            return null;

        TaskCompletionSource<IPipeSignal> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!waiters.TryAdd(reply, completion))
            return null;

        try
        {
            await SignalAsync(request);

            Task finished = await Task.WhenAny(completion.Task, Task.Delay(timeout ?? PipeTransport.GetWaitTimeout()));

            return ReferenceEquals(finished, completion.Task) ? await completion.Task : null;
        }
        finally
        {
            waiters.TryRemove(reply, out _);
        }
    }

    /// <summary>
    /// Reads continuously so unsolicited messages arrive whenever they are sent.
    /// </summary>
    /// <remarks>
    /// Reads used to happen only inside <see cref="ExchangeAsync"/>, which meant the producer could
    /// hear from the consumer solely while parked on a breakpoint. Cancelling a running timeline was
    /// therefore impossible to deliver.
    /// </remarks>
    private async Task ReceiveLoopAsync(PipeProtocolStream ownedStream, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                IPipeSignal? signal = await ownedStream.WaitSignalAsync(cancellationToken);
                if (signal is null)
                    break;

                Dispatch(signal);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
        finally
        {
            FailPendingWaiters();
        }
    }

    private void Dispatch(IPipeSignal signal)
    {
        if (signal.Kind == PipeSignalKind.CancelRun)
        {
            CancellationRequested?.Invoke((signal as PipeCancelRunSignal)?.Reason);
            return;
        }

        if (waiters.TryRemove(signal.Kind, out TaskCompletionSource<IPipeSignal>? completion))
            completion.TrySetResult(signal);
    }

    private void FailPendingWaiters()
    {
        foreach (PipeSignalKind kind in waiters.Keys)
        {
            if (waiters.TryRemove(kind, out TaskCompletionSource<IPipeSignal>? completion))
                completion.TrySetCanceled();
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        DisposeConnection();
        connectionLock.Dispose();
    }

    private async Task<bool> EnsureConnectedAsync()
    {
        if (disposed)
            return false;

        PipeDebuggerMode mode = PipeTransport.GetMode();
        if (mode == PipeDebuggerMode.Off)
            return false;

        if (IsKnownUnavailable(pipeName))
            return false;

        if (IsConnected)
            return true;

        await connectionLock.WaitAsync();
        try
        {
            if (disposed)
                return false;

            if (IsKnownUnavailable(pipeName))
                return false;

            if (IsConnected)
                return true;

            DisposeConnection();

            // CurrentUserOnly validates the server's owner SID, so a hostile pipe squatting on the
            // well-known name under another account cannot receive this run's debug stream.
            NamedPipeClientStream candidate = new(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try
            {
                TimeSpan connectTimeout = PipeTransport.GetConnectTimeout();
                using CancellationTokenSource cts = new(connectTimeout);
                Task connectTask = candidate.ConnectAsync(cts.Token);

                // The token is the primary mechanism, but it is not enough on its own: on Unix a
                // named pipe is a Unix domain socket and ConnectAsync retries internally, so it can
                // overrun the budget by a wide margin before it notices the cancellation. Racing a
                // timer makes the probe cost the same on every platform, which is the whole point
                // of Auto mode. The abandoned task is observed so it cannot resurface unhandled.
                Task finished = await Task.WhenAny(connectTask, Task.Delay(connectTimeout)).ConfigureAwait(false);
                if (!ReferenceEquals(finished, connectTask))
                {
                    ObserveAbandonedConnect(connectTask);
                    throw new TimeoutException($"Connecting to the debug pipe did not finish within {connectTimeout}.");
                }

                await connectTask.ConfigureAwait(false);
            }
            catch
            {
                candidate.Dispose();

                // No latch. The availability probe is cheap enough to repeat, so a UI that starts
                // later is picked up on the next run instead of being locked out for the process.
                return false;
            }

            PipeProtocolStream connectedStream = new(candidate);
            CancellationTokenSource loopCancellation = new();

            lock (stateLock)
            {
                pipeClient = candidate;
                stream = connectedStream;
                receiveCancellation = loopCancellation;
            }

            receiveLoop = Task.Run(() => ReceiveLoopAsync(connectedStream, loopCancellation.Token), CancellationToken.None);

            return true;
        }
        finally
        {
            connectionLock.Release();
        }
    }

    /// <summary>
    /// Swallows the result of a connect attempt the probe stopped waiting for, so an abandoned
    /// task cannot surface later as an unobserved exception.
    /// </summary>
    private static void ObserveAbandonedConnect(Task connectTask)
        => _ = connectTask.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

    private PipeProtocolStream? GetConnectedStream()
    {
        lock (stateLock)
        {
            return pipeClient?.IsConnected == true && stream is not null && !stream.PipeIsDead
                ? stream
                : null;
        }
    }

    private void DisposeConnection(PipeProtocolStream? expectedStream = null)
    {
        NamedPipeClientStream? clientToDispose;
        CancellationTokenSource? loopToCancel;

        lock (stateLock)
        {
            if (expectedStream is not null && !ReferenceEquals(stream, expectedStream))
                return;

            stream = null;
            clientToDispose = pipeClient;
            pipeClient = null;
            loopToCancel = receiveCancellation;
            receiveCancellation = null;
        }

        try
        {
            loopToCancel?.Cancel();
            loopToCancel?.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Already torn down.
        }

        FailPendingWaiters();
        clientToDispose?.Dispose();
    }
}