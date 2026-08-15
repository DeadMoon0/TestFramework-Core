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

internal static class PipeSignalFactory
{
    internal static IPipeSignal DeserializeSignal(string json)
    {
        PipeSignalKind signalKind = (JsonConvert.DeserializeAnonymousType(json, new { Kind = (PipeSignalKind)0 })
            ?? throw new FrameworkStateException("Could not deserialize pipe signal."))
            .Kind;

        return signalKind switch
        {
            PipeSignalKind.InitTimelineRun => JsonConvert.DeserializeObject<PipeInitTimelineRunSignal>(json) ?? throw new FrameworkStateException("Could not deserialize init signal."),
            PipeSignalKind.EntityTransition => JsonConvert.DeserializeObject<PipeEntityTransitionSignal>(json) ?? throw new FrameworkStateException("Could not deserialize entity transition signal."),
            PipeSignalKind.ValueUpdate => JsonConvert.DeserializeObject<PipeValueUpdateSignal>(json) ?? throw new FrameworkStateException("Could not deserialize value update signal."),
            PipeSignalKind.LogEntry => JsonConvert.DeserializeObject<PipeLogEntrySignal>(json) ?? throw new FrameworkStateException("Could not deserialize log entry signal."),
            PipeSignalKind.Assertion => JsonConvert.DeserializeObject<PipeAssertionSignal>(json) ?? throw new FrameworkStateException("Could not deserialize assertion signal."),
            PipeSignalKind.BreakpointHitRequest => JsonConvert.DeserializeObject<PipeBreakpointHitRequestSignal>(json) ?? throw new FrameworkStateException("Could not deserialize breakpoint request signal."),
            PipeSignalKind.BreakpointHitContinue => JsonConvert.DeserializeObject<PipeBreakpointHitContinueSignal>(json) ?? throw new FrameworkStateException("Could not deserialize breakpoint continue signal."),
            PipeSignalKind.TimelineRunFinished => JsonConvert.DeserializeObject<PipeTimelineRunFinishedSignal>(json) ?? throw new FrameworkStateException("Could not deserialize timeline-finished signal."),
            _ => throw new ArgumentOutOfRangeException(nameof(signalKind), signalKind, "Unsupported pipe signal kind.")
        };
    }
}

internal sealed class PipeProtocolStream(PipeStream stream)
{
    private static readonly Encoding WireEncoding = Encoding.Unicode;
    private const int MaxMessageBytes = 4 * 1024 * 1024;
    private readonly SemaphoreSlim sendLock = new(1, 1);

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
            string json = JsonConvert.SerializeObject(signal);
            byte[] buffer = [.. BitConverter.GetBytes(WireEncoding.GetByteCount(json)), .. WireEncoding.GetBytes(json)];

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
        try
        {
            byte[] lenBuf = new byte[sizeof(int)];
            await stream.ReadExactlyAsync(lenBuf, 0, lenBuf.Length, cancellationToken);
            int messageLength = BitConverter.ToInt32(lenBuf);
            if (messageLength <= 0 || messageLength > MaxMessageBytes)
                throw new InvalidDataException($"Invalid pipe frame length: {messageLength}");

            byte[] jsonBuf = new byte[messageLength];
            await stream.ReadExactlyAsync(jsonBuf, 0, jsonBuf.Length, cancellationToken);
            return PipeSignalFactory.DeserializeSignal(WireEncoding.GetString(jsonBuf));
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
    /// Process-wide, because a fresh <see cref="PipeRunDebugger"/> — and therefore a fresh client —
    /// is created for every run. An instance-level flag learned nothing that outlived the run that
    /// paid for it, so every run repeated the connect timeout.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> UnavailablePipes = new(StringComparer.Ordinal);

    /// <summary>
    /// Reports whether this process has already failed to reach the named pipe, so callers can skip
    /// the work of setting up a debugger that has nowhere to send its signals.
    /// </summary>
    internal static bool IsKnownUnavailable(string pipeName) => UnavailablePipes.ContainsKey(pipeName);

    /// <summary>
    /// Clears the process-wide negative cache. Tests that assert on connect behaviour need this
    /// because the cache otherwise makes them depend on the order the suite happens to run in.
    /// </summary>
    internal static void ResetAvailabilityForTests() => UnavailablePipes.Clear();

    private readonly string pipeName;
    private readonly SemaphoreSlim connectionLock = new(1, 1);
    private readonly object stateLock = new();
    private NamedPipeClientStream? pipeClient;
    private PipeProtocolStream? stream;
    private bool disposed;

    internal PipeClient(string pipeName)
    {
        this.pipeName = pipeName;
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
    /// Waits for one signal of the requested kind, or gives up after <paramref name="timeout"/>.
    /// </summary>
    /// <remarks>
    /// On expiry the connection is disposed rather than reused: the frame is half-read, so the next
    /// read would start mid-message and desynchronize framing for the rest of the run.
    /// </remarks>
    internal async Task<IPipeSignal?> WaitForAsync(PipeSignalKind kind, TimeSpan? timeout = null)
    {
        if (!await EnsureConnectedAsync())
            return null;

        PipeProtocolStream? currentStream = GetConnectedStream();
        if (currentStream is null)
            return null;

        IPipeSignal? signal;
        using (CancellationTokenSource waitCancellation = new(timeout ?? PipeTransport.GetWaitTimeout()))
        {
            try
            {
                signal = await currentStream.WaitSignalAsync(waitCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                DisposeConnection(currentStream);
                return null;
            }
        }

        if (signal is null)
        {
            DisposeConnection(currentStream);
            return null;
        }

        if (signal.Kind != kind)
        {
            DisposeConnection(currentStream);
            return null;
        }

        return signal;
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
                using CancellationTokenSource cts = new(PipeTransport.GetConnectTimeout());
                await candidate.ConnectAsync(cts.Token);
            }
            catch
            {
                candidate.Dispose();

                // In On mode a UI is expected and may still be starting, so keep probing. In Auto
                // mode remember the miss: nothing is listening and the whole suite would pay again.
                if (mode != PipeDebuggerMode.On)
                    UnavailablePipes[pipeName] = true;

                return false;
            }

            lock (stateLock)
            {
                pipeClient = candidate;
                stream = new PipeProtocolStream(candidate);
            }

            UnavailablePipes.TryRemove(pipeName, out _);

            return true;
        }
        finally
        {
            connectionLock.Release();
        }
    }

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

        lock (stateLock)
        {
            if (expectedStream is not null && !ReferenceEquals(stream, expectedStream))
                return;

            stream = null;
            clientToDispose = pipeClient;
            pipeClient = null;
        }

        clientToDispose?.Dispose();
    }
}