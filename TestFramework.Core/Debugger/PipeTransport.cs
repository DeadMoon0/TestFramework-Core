using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TestFramework.Core.Debugger;

internal static class PipeTransport
{
    private const string DefaultPipeName = "TestFrameworkDebug_79d7aa2d-da07-4c84-b1f2-0639b0009290";

    internal static string GetPipeName()
    {
        return System.Environment.GetEnvironmentVariable("TESTFRAMEWORK_DEBUG_PIPE_NAME") ?? DefaultPipeName;
    }
}

internal static class PipeSignalFactory
{
    internal static IPipeSignal DeserializeSignal(string json)
    {
        PipeSignalKind signalKind = (JsonConvert.DeserializeAnonymousType(json, new { Kind = (PipeSignalKind)0 })
            ?? throw new InvalidOperationException("Could not deserialize pipe signal."))
            .Kind;

        return signalKind switch
        {
            PipeSignalKind.InitTimelineRun => JsonConvert.DeserializeObject<PipeInitTimelineRunSignal>(json) ?? throw new InvalidOperationException("Could not deserialize init signal."),
            PipeSignalKind.EntityTransition => JsonConvert.DeserializeObject<PipeEntityTransitionSignal>(json) ?? throw new InvalidOperationException("Could not deserialize entity transition signal."),
            PipeSignalKind.ValueUpdate => JsonConvert.DeserializeObject<PipeValueUpdateSignal>(json) ?? throw new InvalidOperationException("Could not deserialize value update signal."),
            PipeSignalKind.LogEntry => JsonConvert.DeserializeObject<PipeLogEntrySignal>(json) ?? throw new InvalidOperationException("Could not deserialize log entry signal."),
            PipeSignalKind.Assertion => JsonConvert.DeserializeObject<PipeAssertionSignal>(json) ?? throw new InvalidOperationException("Could not deserialize assertion signal."),
            PipeSignalKind.BreakpointHitRequest => JsonConvert.DeserializeObject<PipeBreakpointHitRequestSignal>(json) ?? throw new InvalidOperationException("Could not deserialize breakpoint request signal."),
            PipeSignalKind.BreakpointHitContinue => JsonConvert.DeserializeObject<PipeBreakpointHitContinueSignal>(json) ?? throw new InvalidOperationException("Could not deserialize breakpoint continue signal."),
            PipeSignalKind.TimelineRunFinished => JsonConvert.DeserializeObject<PipeTimelineRunFinishedSignal>(json) ?? throw new InvalidOperationException("Could not deserialize timeline-finished signal."),
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
            await stream.WriteAsync(buffer, 0, buffer.Length, CancellationToken.None);
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

    internal async Task<IPipeSignal?> WaitForAsync(PipeSignalKind kind)
    {
        if (!await EnsureConnectedAsync())
            return null;

        PipeProtocolStream? currentStream = GetConnectedStream();
        if (currentStream is null)
            return null;

        IPipeSignal? signal = await currentStream.WaitSignalAsync();
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

        if (IsConnected)
            return true;

        await connectionLock.WaitAsync();
        try
        {
            if (disposed)
                return false;

            if (IsConnected)
                return true;

            DisposeConnection();

            NamedPipeClientStream candidate = new(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                using CancellationTokenSource cts = new();
                cts.CancelAfter(TimeSpan.FromSeconds(2));
                await candidate.ConnectAsync(cts.Token);
            }
            catch
            {
                candidate.Dispose();
                return false;
            }

            lock (stateLock)
            {
                pipeClient = candidate;
                stream = new PipeProtocolStream(candidate);
            }

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