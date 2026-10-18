using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace TestFramework.Core.Debugger;

internal sealed class PipeProtocolStream(PipeStream stream, EnvelopeSequence sequence)
{
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

            byte[] buffer = DebugEnvelopeCodec.EncodeFrame(
                DebugEnvelopeCodec.Wrap(signal, sequence.Next()));

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
