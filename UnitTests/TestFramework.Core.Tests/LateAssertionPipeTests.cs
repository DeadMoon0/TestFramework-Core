using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Debugger;
using TestFramework.Core.Timelines;

namespace TestFramework.Core.Tests;

/// <summary>
/// A check made on a finished run reaches a watching UI over the pipe, after the run, numbered on from it.
/// </summary>
/// <remarks>
/// End to end through the real transport, because each half was fine on its own: the session handed the check to
/// the pipe, and the pipe had already been disposed. The UI drops a frame whose number it has already seen, so
/// the late one must carry on from the run's own numbering rather than start again.
/// </remarks>
[Collection("DebuggerEnvironment")]
public sealed class LateAssertionPipeTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task ACheckOnTheFinishedRunArrivesOnItsOwnConnection_NumberedAfterTheFinish()
    {
        string pipeName = "tf-late-" + Guid.NewGuid().ToString("N");
        string? previousName = System.Environment.GetEnvironmentVariable("TESTFRAMEWORK_DEBUG_PIPE_NAME");
        string? previousMode = System.Environment.GetEnvironmentVariable("TESTFRAMEWORK_DEBUG_PIPE");
        System.Environment.SetEnvironmentVariable("TESTFRAMEWORK_DEBUG_PIPE_NAME", pipeName);
        System.Environment.SetEnvironmentVariable("TESTFRAMEWORK_DEBUG_PIPE", "on");

        try
        {
            using CancellationTokenSource life = new(Patience);

            Task<List<DebugEnvelope>> runConnection = ReadOneConnectionAsync(pipeName, untilFinished: true, life.Token);

            TimelineRun run = await Timeline.Create()
                .SetVariable("count", 6)
                .Build()
                .SetupRun()
                .RunAsync();

            List<DebugEnvelope> runFrames = await runConnection;
            Assert.Equal(PipeSignalKind.TimelineRunFinished, runFrames[^1].Kind);

            Task<List<DebugEnvelope>> lateConnection = ReadOneConnectionAsync(pipeName, untilFinished: false, life.Token);

            run.Variable<int>("count").Should().Be(6);

            DebugEnvelope late = Assert.Single(await lateConnection);
            Assert.Equal(PipeSignalKind.Assertion, late.Kind);
            Assert.Equal(runFrames[^1].SessionId, late.SessionId);
            Assert.True(late.Seq > runFrames[^1].Seq, "The late check must be numbered after the run's finish, or the UI drops it as already seen.");
        }
        finally
        {
            System.Environment.SetEnvironmentVariable("TESTFRAMEWORK_DEBUG_PIPE_NAME", previousName);
            System.Environment.SetEnvironmentVariable("TESTFRAMEWORK_DEBUG_PIPE", previousMode);
        }
    }

    /// <summary>Plays the UI for one connection: takes it, lets every step go, and reads what else it carries.</summary>
    private static async Task<List<DebugEnvelope>> ReadOneConnectionAsync(string pipeName, bool untilFinished, CancellationToken cancellationToken)
    {
        await using NamedPipeServerStream server = new(
            pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

        await server.WaitForConnectionAsync(cancellationToken);

        PipeProtocolStream framing = new(server, new EnvelopeSequence());
        List<DebugEnvelope> frames = [];

        while (await framing.WaitEnvelopeAsync(cancellationToken) is { } envelope)
        {
            // Every step asks whether to pause; a UI that never answered would hold each one for the full wait.
            if (DebugEnvelopeCodec.Unwrap(envelope) is PipeBreakpointHitRequestSignal request)
            {
                await framing.SendSignalAsync(new PipeBreakpointHitContinueSignal { SessionId = request.SessionId });
                continue;
            }

            frames.Add(envelope);

            if (untilFinished && envelope.Kind == PipeSignalKind.TimelineRunFinished)
                break;
        }

        return frames;
    }
}
