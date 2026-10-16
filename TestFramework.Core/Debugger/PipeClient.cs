using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace TestFramework.Core.Debugger;

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

    /// <summary>
    /// Answers the consumer's request for fresh evidence, when the run has anything to answer with.
    /// </summary>
    /// <remarks>
    /// One handler rather than an event, because this request has a reply: the consumer is told what
    /// came of it, and an event returning a value to a multicast list has no honest answer to give.
    /// </remarks>
    internal Func<Task<WidgetCaptureOutcome>>? OnCaptureRequested { get; set; }

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

        if (signal.Kind == PipeSignalKind.CaptureWidgetRequest)
        {
            AnswerCaptureRequest(signal.SessionId);
            return;
        }

        if (waiters.TryRemove(signal.Kind, out TaskCompletionSource<IPipeSignal>? completion))
            completion.TrySetResult(signal);
    }

    /// <summary>
    /// Captures what the consumer asked for, and answers with what came of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately off the receive loop. A capture photographs a live page — I/O measured in
    /// hundreds of milliseconds — and this loop is the only thing that can hear the continue
    /// releasing the very breakpoint the request was sent from. Serving the picture on it would hold
    /// the run to get a look at the run.
    /// </para>
    /// <para>
    /// The answer is sent whatever happened, including when nothing here can capture anything. A
    /// consumer that pressed a button is owed a reply; one that gets silence has to wait out its own
    /// timeout to learn that the run was never going to answer.
    /// </para>
    /// </remarks>
    private void AnswerCaptureRequest(string sessionId)
    {
        Func<Task<WidgetCaptureOutcome>>? capture = OnCaptureRequested;

        _ = Task.Run(async () =>
        {
            WidgetCaptureOutcome outcome = capture is null
                ? new WidgetCaptureOutcome(0, "This run has nothing registered that can capture evidence.")
                : await CaptureSafelyAsync(capture);

            try
            {
                await SignalAsync(new PipeCaptureWidgetAckSignal
                {
                    SessionId = sessionId,
                    Captured = outcome.Captured,
                    Detail = outcome.Detail
                });
            }
            catch (Exception e)
            {
                // The consumer went away while its own picture was being taken. It will time out,
                // which is the same outcome it would have had, and the run carries on regardless.
                Debug.WriteLine(e);
            }
        });
    }

    private static async Task<WidgetCaptureOutcome> CaptureSafelyAsync(Func<Task<WidgetCaptureOutcome>> capture)
    {
        try
        {
            return await capture();
        }
        catch (Exception e)
        {
            // Evidence gathering must not be able to take a run down, and least of all one a person
            // is standing at a breakpoint watching.
            Debug.WriteLine(e);
            return new WidgetCaptureOutcome(0, e.Message);
        }
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
